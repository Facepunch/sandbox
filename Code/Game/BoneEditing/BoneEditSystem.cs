using Sandbox.Npcs;

/// <summary>
/// A system for editing bones on skinned props. 
/// </summary>
public sealed class BoneEditSystem : GameObjectSystem<BoneEditSystem>, IContextMenuEvent
{
	public enum GizmoMode
	{
		Move,
		Rotate,
		Scale
	}

	const float PickRadius = 14.0f;
	const float SendInterval = 1.0f / 20.0f;
	const float MaxOffsetDistance = 256.0f;
	const float MinScale = 0.05f;
	const float MaxScale = 10.0f;

	static readonly Color BoneColor = new( 0.6f, 0.6f, 0.6f, 0.5f );
	static readonly Color HoveredColor = new( 0.9f, 0.9f, 0.9f );
	static readonly Color SelectedColor = new( 1.0f, 0.6f, 0.1f );
	const int GradientSteps = 8;

	/// <summary>
	/// The renderers being edited.
	/// </summary>
	public IReadOnlyList<SkinnedModelRenderer> Targets => _targets;

	/// <summary>
	/// The renderer the selected joint belongs to, or null.
	/// </summary>
	public SkinnedModelRenderer SelectedRenderer { get; private set; }
	public BoneCollection.Bone SelectedBone { get; private set; }

	public SkinnedModelRenderer HoveredRenderer { get; private set; }
	public BoneCollection.Bone HoveredBone { get; private set; }

	public GizmoMode Mode { get; set; }

	readonly List<SkinnedModelRenderer> _targets = [];
	readonly Dictionary<SkinnedModelRenderer, Transform[]> _boneWorld = [];
	bool _mouseDown;
	bool _selectionClick;
	bool _gizmoHovered;
	readonly TransformGizmo _gizmo = new();

	SkinnedModelRenderer _pendingRenderer;
	string _pendingBone;
	Transform _pendingOffset;
	TimeSince _sinceSend;

	public BoneEditSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, Tick, "BoneEditor" );
	}

	static bool IsInspecting => SpawnMenuHost.IsOpen && SpawnMenuHost.GetActiveMode() is ContextMenuHost;

	public bool IsEditing( SkinnedModelRenderer smr ) => smr is not null && _targets.Contains( smr );

	public void Begin( SkinnedModelRenderer target )
	{
		if ( !target.IsValid() || IsEditing( target ) ) return;

		_targets.Add( target );
		Sound.Play( "ui.button.press" );
	}

	public void Finish( SkinnedModelRenderer target )
	{
		if ( !_targets.Remove( target ) ) return;

		_boneWorld.Remove( target );

		if ( SelectedRenderer == target )
			Select( null, null );

		if ( _pendingRenderer == target )
			FlushPendingOffset( true );

		if ( HoveredRenderer == target )
		{
			HoveredRenderer = null;
			HoveredBone = null;
		}
	}

	void Select( SkinnedModelRenderer smr, BoneCollection.Bone bone )
	{
		if ( SelectedRenderer == smr && SelectedBone == bone ) return;

		// pending edits belong to the joint being left
		FlushPendingOffset( true );

		SelectedRenderer = smr;
		SelectedBone = bone;

		_mouseDown = false;
		_selectionClick = false;
		_gizmoHovered = false;
		_gizmo.Cancel();
	}

	/// <summary>
	/// A left click on the world while the inspect menu is open. Returns true when the click was ours, on the gizmo or
	/// on a joint. Anything else deselects the joint and goes on to the inspector.
	/// </summary>
	public bool WorldMouseDown()
	{
		// the press belongs to the gizmo handle under the cursor
		if ( _gizmoHovered )
		{
			_mouseDown = true;
			return true;
		}

		if ( HoveredBone is null )
		{
			Select( null, null );
			return false;
		}

		if ( SelectedRenderer != HoveredRenderer || SelectedBone != HoveredBone )
		{
			Select( HoveredRenderer, HoveredBone );
			Sound.Play( "ui.button.press" );
		}

		// a click that picks a joint mustn't also grab the gizmo that appears under the cursor
		_mouseDown = true;
		_selectionClick = true;
		return true;
	}

	public void WorldMouseUp()
	{
		_mouseDown = false;
		_selectionClick = false;
	}

	void Tick()
	{
		for ( int i = _targets.Count - 1; i >= 0; i-- )
		{
			if ( !_targets[i].IsValid() || _targets[i].Model is null )
				Finish( _targets[i] );
		}

		if ( _targets.Count == 0 || !IsInspecting || Scene.Camera is not { IsValid: true } camera )
		{
			FlushPendingOffset( true );
			_mouseDown = false;
			_selectionClick = false;
			_gizmoHovered = false;
			HoveredRenderer = null;
			HoveredBone = null;
			_gizmo.Cancel();
			return;
		}

		_gizmo.Update( camera, Mouse.Position, _mouseDown && !_selectionClick );

		foreach ( var target in _targets )
			ReadBones( target );

		if ( !_mouseDown )
			FindBoneUnderCursor( camera );

		foreach ( var target in _targets )
			DrawSkeleton( camera, target );

		if ( SelectedBone is not null )
			DrawControl();

		_gizmoHovered = _gizmo.IsHovered;

		FlushPendingOffset( !_mouseDown );
	}

	void ReadBones( SkinnedModelRenderer target )
	{
		var bones = target.Model.Bones.AllBones;

		if ( !_boneWorld.TryGetValue( target, out var world ) || world.Length != bones.Count )
		{
			world = new Transform[bones.Count];
			_boneWorld[target] = world;
		}

		for ( int i = 0; i < bones.Count; i++ )
		{
			if ( !target.TryGetBoneTransform( bones[i], out world[i] ) )
				world[i] = target.WorldTransform;
		}
	}

	/// <summary>
	/// The joint closest to the cursor on any of the renderers being edited.
	/// </summary>
	void FindBoneUnderCursor( CameraComponent camera )
	{
		var cursor = Mouse.Position;
		var bestDistance = PickRadius;

		HoveredRenderer = null;
		HoveredBone = null;

		foreach ( var target in _targets )
		{
			var bones = target.Model.Bones.AllBones;
			var world = _boneWorld[target];

			for ( int i = 0; i < bones.Count; i++ )
			{
				var screen = camera.PointToScreenPixels( world[i].Position, out var behind );
				if ( behind ) continue;

				var distance = screen.Distance( cursor );
				if ( distance >= bestDistance ) continue;

				bestDistance = distance;
				HoveredRenderer = target;
				HoveredBone = bones[i];
			}
		}
	}

	void DrawSkeleton( CameraComponent camera, SkinnedModelRenderer target )
	{
		var bones = target.Model.Bones.AllBones;
		var world = _boneWorld[target];
		var selectedBone = SelectedRenderer == target ? SelectedBone : null;
		var hoveredBone = HoveredRenderer == target ? HoveredBone : null;

		using var scope = Gizmo.Scope( "bone-editor" );

		Gizmo.Draw.IgnoreDepth = true;
		Gizmo.Draw.LineThickness = 2;

		for ( int i = 0; i < bones.Count; i++ )
		{
			var bone = bones[i];
			if ( bone.Parent is not { } parent )
				continue;

			var start = world[parent.Index].Position;
			var end = world[i].Position;

			if ( parent == selectedBone )
				DrawGradientLine( start, end );
			else if ( bone == selectedBone )
				DrawGradientLine( end, start );
			else
			{
				Gizmo.Draw.Color = BoneColor;
				Gizmo.Draw.Line( start, end );
			}
		}

		for ( int i = 0; i < bones.Count; i++ )
		{
			var bone = bones[i];
			var position = world[i].Position;
			var selected = bone == selectedBone;
			var hovered = bone == hoveredBone;

			Gizmo.Draw.Color = selected ? SelectedColor : hovered ? HoveredColor : BoneColor;
			var radius = camera.WorldPosition.Distance( position ) * (selected || hovered ? 0.006f : 0.004f);
			Gizmo.Draw.SolidSphere( position, radius, 6, 6 );
		}

		var labelled = hoveredBone ?? selectedBone;
		if ( labelled is not null && (hoveredBone is not null || HoveredBone is null) )
		{
			Gizmo.Draw.Color = Color.White;
			Gizmo.Draw.ScreenText( labelled.Name, world[labelled.Index].Position, new Vector2( 12, -18 ), size: 14 );
		}
	}

	static void DrawGradientLine( Vector3 from, Vector3 to )
	{
		for ( int i = 0; i < GradientSteps; i++ )
		{
			var a = (float)i / GradientSteps;
			var b = (float)(i + 1) / GradientSteps;

			Gizmo.Draw.Color = Color.Lerp( SelectedColor, BoneColor, (a + b) * 0.5f );
			Gizmo.Draw.Line( Vector3.Lerp( from, to, a ), Vector3.Lerp( from, to, b ) );
		}
	}

	void DrawControl()
	{
		var target = SelectedRenderer;
		var bone = SelectedBone;
		var boneWorld = _boneWorld[target];
		var world = boneWorld[bone.Index];
		var parentWorld = bone.Parent is { } parent ? boneWorld[parent.Index] : target.WorldTransform;

		target.TryGetBoneOffset( bone, out var offset );

		switch ( Mode )
		{
			case GizmoMode.Move:
				// Offset positions are in the parent bone's space, so are the handles
				if ( _gizmo.Move( world.Position, parentWorld.Rotation, out var moved ) )
				{
					var delta = parentWorld.PointToLocal( moved ) - parentWorld.PointToLocal( world.Position );

					// keep within what the host accepts, or this client would drift from everyone else
					var position = offset.Position + delta;
					if ( position.Length > MaxOffsetDistance )
						position = position.Normal * MaxOffsetDistance;

					SetOffset( target, bone, offset.WithPosition( position ) );
				}
				break;

			case GizmoMode.Rotate:
				// Offset rotations are in the bone's own space, so the rings' change in that space composes straight on
				if ( _gizmo.Rotate( world.Position, world.Rotation, out var rotated ) )
				{
					var delta = world.Rotation.Inverse * rotated;
					SetOffset( target, bone, offset.WithRotation( (offset.Rotation * delta).Normal ) );
				}
				break;

			case GizmoMode.Scale:
				if ( _gizmo.Scale( world.Position, world.Rotation, offset.Scale.x, out var scale ) )
				{
					SetOffset( target, bone, offset.WithScale( scale.Clamp( MinScale, MaxScale ) ) );
				}
				break;
		}
	}

	void SetOffset( SkinnedModelRenderer target, BoneCollection.Bone bone, Transform offset )
	{
		target.SetBoneOffset( bone, offset );

		_pendingRenderer = target;
		_pendingBone = bone.Name;
		_pendingOffset = offset;
	}

	void FlushPendingOffset( bool force )
	{
		if ( _pendingBone is null ) return;
		if ( !force && _sinceSend < SendInterval ) return;

		if ( _pendingRenderer.IsValid() )
			SendBoneOffset( _pendingRenderer, _pendingBone, _pendingOffset );

		_pendingRenderer = null;
		_pendingBone = null;
		_sinceSend = 0;
	}

	void ResetBone( SkinnedModelRenderer smr, string bone )
	{
		if ( _pendingRenderer == smr )
		{
			_pendingRenderer = null;
			_pendingBone = null;
		}

		ClearOffsets( smr, bone );
		SendClearBoneOffsets( smr, bone ?? "" );
	}

	void IContextMenuEvent.PopulateContextMenu( IContextMenuEvent.Event e )
	{
		var smr = FindRenderer( e.Target );
		if ( !smr.IsValid() || !CanEdit( smr, Connection.Local ) ) return;

		// The bone options go together between lines, the menu drops any it doesn't need
		e.AddSeparator( 249 );
		e.AddSeparator( 254 );

		if ( IsEditing( smr ) )
		{
			e.AddOption( "done", "#spawnmenu.inspect.finish_editing", () => Finish( smr ), 250 );

			if ( SelectedRenderer == smr && SelectedBone is { } bone && smr.TryGetBoneOffset( bone, out _ ) )
			{
				e.AddOption( "undo", "#spawnmenu.inspect.reset_bone", () => ResetBone( smr, bone.Name ), 252 );
			}
		}
		else
		{
			e.AddOption( "accessibility_new", "#spawnmenu.inspect.edit_bones", () => Begin( smr ), 250 );
		}

		if ( smr.HasBoneOffsets )
		{
			e.AddOption( "restart_alt", "#spawnmenu.inspect.reset_bones", () => ResetBone( smr, null ), 253 );
		}
	}

	/// <summary>
	/// The renderer that owns the skeleton of an inspected object: the body of a player or NPC, or the
	/// first skinned renderer that isn't bone merged onto another one.
	/// </summary>
	public static SkinnedModelRenderer FindRenderer( GameObject root )
	{
		if ( !root.IsValid() ) return null;

		if ( root.GetComponent<Player>() is { } player )
			return HasSkeleton( player.Controller?.Renderer ) ? player.Controller.Renderer : null;

		if ( root.GetComponent<Npc>() is { } npc )
			return HasSkeleton( npc.Renderer ) ? npc.Renderer : null;

		foreach ( var smr in root.GetComponentsInChildren<SkinnedModelRenderer>() )
		{
			if ( smr.BoneMergeTarget.IsValid() ) continue;
			if ( HasSkeleton( smr ) ) return smr;
		}

		return null;
	}

	static bool HasSkeleton( SkinnedModelRenderer smr ) => smr.IsValid() && smr.Model is { BoneCount: > 1 };

	/// <summary>
	/// Players can only edit their own body, everything else follows the usual ownership rules.
	/// </summary>
	public static bool CanEdit( SkinnedModelRenderer smr, Connection caller )
	{
		if ( !smr.IsValid() || caller is null ) return false;

		if ( smr.GetComponentInParent<Player>() is { } player )
			return player.Network.Owner == caller || caller.HasPermission( "admin" );

		var root = smr.GameObject.FindNetworkRoot() ?? smr.GameObject.Root;
		return root.HasAccess( caller );
	}

	static bool IsSaneOffset( in Transform offset )
	{
		var p = offset.Position;
		if ( !float.IsFinite( p.x ) || !float.IsFinite( p.y ) || !float.IsFinite( p.z ) || p.Length > MaxOffsetDistance ) return false;

		var r = offset.Rotation;
		if ( !float.IsFinite( r.x ) || !float.IsFinite( r.y ) || !float.IsFinite( r.z ) || !float.IsFinite( r.w ) ) return false;

		var lengthSquared = r.x * r.x + r.y * r.y + r.z * r.z + r.w * r.w;
		if ( MathF.Abs( lengthSquared - 1.0f ) > 0.01f ) return false;

		// bone scale is uniform, anything else is a hand-made transform
		var s = offset.Scale;
		if ( !float.IsFinite( s.x ) || s.x < MinScale || s.x > MaxScale ) return false;
		return MathF.Abs( s.y - s.x ) < 0.0001f && MathF.Abs( s.z - s.x ) < 0.0001f;
	}

	[Rpc.Host]
	static void SendBoneOffset( SkinnedModelRenderer smr, string bone, Transform offset )
	{
		if ( !CanEdit( smr, Rpc.Caller ) ) return;
		if ( string.IsNullOrEmpty( bone ) || smr.Model?.Bones.HasBone( bone ) != true ) return;
		if ( !IsSaneOffset( offset ) ) return;

		smr.SetBoneOffset( bone, offset );
		BroadcastBoneOffset( smr, bone, offset, Rpc.Caller.Id );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void BroadcastBoneOffset( SkinnedModelRenderer smr, string bone, Transform offset, Guid editor )
	{
		if ( Networking.IsHost || !smr.IsValid() ) return;

		// the editor already shows its own edit, and a late echo would drag the bone back mid-drag
		if ( editor == Connection.Local.Id ) return;

		smr.SetBoneOffset( bone, offset );
	}

	/// <summary>
	/// Clear one bone's offset, or all of them with an empty bone name.
	/// </summary>
	[Rpc.Host]
	static void SendClearBoneOffsets( SkinnedModelRenderer smr, string bone )
	{
		if ( !CanEdit( smr, Rpc.Caller ) ) return;

		ClearOffsets( smr, bone );
		BroadcastClearBoneOffsets( smr, bone );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void BroadcastClearBoneOffsets( SkinnedModelRenderer smr, string bone )
	{
		if ( Networking.IsHost || !smr.IsValid() ) return;

		ClearOffsets( smr, bone );
	}

	static void ClearOffsets( SkinnedModelRenderer smr, string bone )
	{
		if ( string.IsNullOrEmpty( bone ) )
			smr.ClearBoneOffsets();
		else
			smr.ClearBoneOffset( bone );
	}
}
