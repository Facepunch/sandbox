[Icon( "🦴" )]
[Title( "#tool.name.bonemerger" )]
[ClassName( "bonemerger" )]
[Group( "#tool.group.constraints" )]
public sealed class BoneMergerTool : ToolMode
{
	int _stage;
	SelectionPoint _source;
	SelectionPoint _target;
	SkinnedModelRenderer _targetRenderer;
	string _boneName;

	/// <summary>
	/// Picked with secondary: the prop stays where it is and is only parented onto the bone.
	/// </summary>
	bool _inPlace;

	Rotation _rotation = Rotation.Identity;
	Rotation _rawRotation = Rotation.Identity;
	bool _isSnapping;

	readonly PlacementPreview _preview = new();

	public override bool UseSnapGrid => _stage == 1;
	public override bool UseBoneSnap => true;
	public override bool AbsorbMouseInput => _stage == 2;

	public override string Description => _stage switch
	{
		2 => "#tool.hint.bonemerger.stage2",
		1 => _inPlace ? "#tool.hint.bonemerger.stage1_in_place" : "#tool.hint.bonemerger.stage1",
		_ => "#tool.hint.bonemerger.stage0"
	};

	public override string PrimaryAction => _stage switch
	{
		2 => "#tool.hint.bonemerger.confirm",
		1 => _inPlace ? "#tool.hint.bonemerger.confirm" : "#tool.hint.bonemerger.bone",
		_ => "#tool.hint.bonemerger.source"
	};

	public override string SecondaryAction => _stage > 0 ? "#tool.hint.bonemerger.cancel" : "#tool.hint.bonemerger.in_place";
	public override string ReloadAction => _stage == 0 ? "#tool.hint.bonemerger.remove" : null;

	public override void OnControl()
	{
		if ( _stage < 2 )
			base.OnControl();

		ControlStages();

		_preview.EndFrame();
		Toolgun.SetIsUsingJoystick( _stage == 2 );
	}

	void ControlStages()
	{
		if ( _stage > 0 && (Input.Pressed( "attack2" ) || !_source.IsValid()) )
		{
			Cancel();
			return;
		}

		switch ( _stage )
		{
			case 0: PickSource(); break;
			case 1: PickBone(); break;
			case 2: RotateStage(); break;
		}
	}

	void PickSource()
	{
		var select = TraceSelect();
		var root = PropRoot( select );
		IsValidState = root.IsValid() && CanMergeSource( root );

		if ( Input.Pressed( "reload" ) && BoneMerge.IsMerged( root ) )
		{
			if ( !FireToolAction( ToolInput.Reload ) ) return;
			Unmerge( root );
			ShootEffects( select );
			FirePostToolAction( ToolInput.Reload );
			return;
		}

		if ( !IsValidState ) return;

		var inPlace = Input.Pressed( "attack2" );
		if ( !inPlace && !Input.Pressed( "attack1" ) ) return;

		_source = select;
		_inPlace = inPlace;
		_stage = 1;
		ShootEffects( select );
	}

	void PickBone()
	{
		var select = TraceSelect();
		var sourceRoot = PropRoot( _source );

		IsValidState = select.IsValid() && SnapRenderer.IsValid() && SnapBone is not null
			&& BoneEditSystem.CanEdit( SnapRenderer, Connection.Local )
			&& !IsInside( SnapRenderer.GameObject, sourceRoot );

		if ( !IsValidState ) return;

		if ( _inPlace )
		{
			if ( !Input.Pressed( "attack1" ) || !FireToolAction( ToolInput.Primary ) ) return;

			MergeInPlace( sourceRoot, SnapRenderer, SnapBone.Name );
			ShootEffects( select );
			FirePostToolAction( ToolInput.Primary );
			Cancel();
			return;
		}

		if ( Input.Down( "snap" ) )
		{
			var lt = select.LocalTransform;
			lt.Position = select.GameObject.WorldTransform.ToLocal( new Transform( SnapWorldPosition ) ).Position;
			select.LocalTransform = lt;
		}

		_preview.Draw( Scene, sourceRoot, GetEasyModePlacement( _source, select ), [sourceRoot] );

		if ( !Input.Pressed( "attack1" ) ) return;

		_target = select;
		_targetRenderer = SnapRenderer;
		_boneName = SnapBone.Name;
		ResetRotation();
		_stage = 2;
		SnapGrid?.Hide();
		ShootEffects( select );
	}

	void RotateStage()
	{
		if ( !_target.IsValid() || !_targetRenderer.IsValid() )
		{
			Cancel();
			return;
		}

		UpdateRotationInput();

		var sourceRoot = PropRoot( _source );
		_preview.Draw( Scene, sourceRoot, GetRotatedPlacement( _source, _target, _rotation ), [sourceRoot] );

		if ( !Input.Pressed( "attack1" ) ) return;
		if ( !FireToolAction( ToolInput.Primary ) ) return;

		Merge( _source, _target, _targetRenderer, _boneName, _rotation );
		ShootEffects( _target );
		FirePostToolAction( ToolInput.Primary );
		Cancel();
	}

	void UpdateRotationInput()
	{
		var look = Input.AnalogLook;
		var view = Toolgun.Owner.EyeTransform.Rotation;

		_rawRotation = (Rotation.FromAxis( view.Up, -look.yaw ) * Rotation.FromAxis( view.Right, look.pitch ) * _rawRotation).Normal;

		var isSnapping = Input.Down( "run" );
		if ( !isSnapping && _isSnapping ) _rawRotation = _rotation;
		_isSnapping = isSnapping;

		_rotation = _isSnapping ? SnapRotation( _rawRotation, _target.WorldTransform().Rotation ) : _rawRotation;

		Toolgun.UpdateJoystick( new Angles( look.pitch, look.yaw, 0 ) );
	}

	static Rotation SnapRotation( Rotation rotation, Rotation surface )
	{
		var local = (surface.Inverse * rotation * surface).Angles();
		var snapped = new Angles( MathF.Round( local.pitch / 15f ) * 15f, MathF.Round( local.yaw / 15f ) * 15f, MathF.Round( local.roll / 15f ) * 15f );
		return surface * snapped.ToRotation() * surface.Inverse;
	}

	void ResetRotation()
	{
		_rotation = Rotation.Identity;
		_rawRotation = Rotation.Identity;
		_isSnapping = false;
	}

	void Cancel()
	{
		_stage = 0;
		_source = default;
		_target = default;
		_targetRenderer = null;
		_boneName = null;
		_inPlace = false;
		ResetRotation();
		SnapGrid?.Hide();
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		Cancel();
		_preview.Clear();
	}

	Transform GetRotatedPlacement( SelectionPoint source, SelectionPoint target, Rotation rotation )
	{
		var placement = GetEasyModePlacement( source, target );

		var contactPoint = target.WorldPosition();
		placement.Position = contactPoint + rotation * (placement.Position - contactPoint);
		placement.Rotation = rotation * placement.Rotation;

		return placement;
	}

	static GameObject PropRoot( SelectionPoint select )
	{
		if ( !select.IsValid() || select.IsWorld ) return null;
		return select.GameObject.Network.RootGameObject ?? select.GameObject;
	}

	static bool CanMergeSource( GameObject root )
	{
		if ( !root.IsValid() || root.Tags.Has( "player" ) ) return false;
		if ( root.GetComponent<Player>() is not null || root.GetComponent<Sandbox.Npcs.Npc>() is not null ) return false;

		return root.GetComponentInChildren<ModelRenderer>() is not null;
	}

	static bool IsInside( GameObject go, GameObject ancestor )
	{
		for ( var o = go; o.IsValid() && o is not Sandbox.Scene; o = o.Parent )
		{
			if ( o == ancestor ) return true;
		}

		return false;
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void Merge( SelectionPoint source, SelectionPoint target, SkinnedModelRenderer renderer, string boneName, Rotation rotation )
	{
		var prop = PropRoot( source );
		var caller = Rpc.Caller ?? Connection.Local;

		if ( !prop.IsValid() || !target.IsValid() || !renderer.IsValid() || string.IsNullOrEmpty( boneName ) ) return;
		if ( !CanMergeSource( prop ) || !prop.HasAccess( caller ) ) return;
		if ( !BoneEditSystem.CanEdit( renderer, caller ) || IsInside( renderer.GameObject, prop ) ) return;

		// Placement is part of this RPC, independent of the client's current input state
		Attach( prop, renderer, boneName, GetRotatedPlacement( source, target, rotation ) );
	}

	/// <summary>
	/// Parent the prop onto the bone right where it is.
	/// </summary>
	[Rpc.Host( NetFlags.OwnerOnly )]
	void MergeInPlace( GameObject prop, SkinnedModelRenderer renderer, string boneName )
	{
		var caller = Rpc.Caller ?? Connection.Local;

		if ( !prop.IsValid() || !renderer.IsValid() || string.IsNullOrEmpty( boneName ) ) return;
		if ( !CanMergeSource( prop ) || !prop.HasAccess( caller ) ) return;
		if ( !BoneEditSystem.CanEdit( renderer, caller ) || IsInside( renderer.GameObject, prop ) ) return;

		Attach( prop, renderer, boneName, prop.WorldTransform );
	}

	void Attach( GameObject prop, SkinnedModelRenderer renderer, string boneName, Transform placement )
	{
		var link = BoneMerge.Attach( prop, renderer, boneName, placement );
		if ( !link.IsValid() ) return;

		Track( link );

		var undo = Player.Undo.Create();
		undo.Name = "#tool.name.bonemerger";
		undo.Add( link );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void Unmerge( GameObject prop )
	{
		if ( !prop.IsValid() || !prop.HasAccess( Rpc.Caller ?? Connection.Local ) ) return;

		BoneMerge.Release( prop );
	}
}
