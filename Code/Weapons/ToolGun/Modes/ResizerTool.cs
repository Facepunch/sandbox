[Icon( "🍄" )]
[Title( "#tool.name.resizer" )]
[ClassName( "resizer" )]
[Group( "#tool.group.tools" )]
public sealed class ResizerTool : ToolMode
{
	const float Step = 0.033f;

	public override IEnumerable<string> TraceIgnoreTags => [];

	public override string Description => "#tool.hint.resizer.description";

	public override string SnapAction => "#tool.hint.resizer.whole";

	TimeSince timeSinceAction = 0;

	protected override void OnStart()
	{
		base.OnStart();

		RegisterAction( ToolInput.Primary, () => "#tool.hint.resizer.grow", OnGrow, InputMode.Down );
		RegisterAction( ToolInput.Secondary, () => "#tool.hint.resizer.shrink", OnShrink, InputMode.Down );
		RegisterAction( ToolInput.Reload, () => "#tool.hint.resizer.reset", OnReset );
	}

	void OnGrow() => OnResize( Step );
	void OnShrink() => OnResize( -Step );

	void OnResize( float size )
	{
		var select = TraceSelect();
		IsValidState = select.IsValid() && !select.IsWorld;
		if ( !IsValidState ) return;
		if ( timeSinceAction < 0.03f ) return;

		if ( IsBoneTarget( select, out var smr ) )
		{
			var bone = FindAimedBone( smr );
			IsValidState = bone is not null;
			if ( !IsValidState ) return;

			ResizeBone( smr, bone, size );
		}
		else
		{
			Resize( select.GameObject, size );
		}

		timeSinceAction = 0;
	}

	void OnReset()
	{
		var select = TraceSelect();
		IsValidState = select.IsValid() && !select.IsWorld;
		if ( !IsValidState ) return;

		if ( IsBoneTarget( select, out var smr ) )
		{
			var bone = FindAimedBone( smr );
			IsValidState = bone is not null;
			if ( !IsValidState ) return;

			ResetBone( smr, bone );
		}
		else
		{
			ResetScale( select.GameObject );
		}

		ShootEffects( select );
	}

	/// <summary>
	/// Characters and ragdolls are resized a bone at a time, unless snap is held to resize the whole object.
	/// </summary>
	bool IsBoneTarget( SelectionPoint select, out SkinnedModelRenderer smr )
	{
		smr = null;

		if ( Input.Down( "snap" ) ) return false;

		var root = select.GameObject.FindNetworkRoot() ?? select.GameObject.Root;
		smr = BoneEditSystem.FindRenderer( root );
		if ( !smr.IsValid() || !BoneEditSystem.CanEdit( smr, Connection.Local ) ) return false;

		// Something to aim at: hitboxes on characters, bodies on ragdolls
		return root.GetComponentInChildren<ModelHitboxes>() is not null || smr.GetComponent<ModelPhysics>() is { Bodies.Count: > 0 };
	}

	/// <summary>
	/// The bone under the aim, through hitboxes on characters and body colliders on ragdolls.
	/// </summary>
	BoneCollection.Bone FindAimedBone( SkinnedModelRenderer smr )
	{
		var player = Toolgun?.Owner;
		if ( !player.IsValid() ) return null;

		var root = smr.GameObject.FindNetworkRoot() ?? smr.GameObject.Root;
		BoneCollection.Bone bone = null;

		// Hitboxes sit inside the movement collider, skip it the way bullets do
		var tr = Scene.Trace.Ray( player.EyeTransform.ForwardRay, 4096 )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.WithoutTags( "playercontroller" )
			.UseHitboxes()
			.Run();

		if ( !tr.Hit || !tr.GameObject.IsValid() ) return null;
		if ( (tr.GameObject.FindNetworkRoot() ?? tr.GameObject.Root) != root ) return null;

		if ( tr.Hitbox?.Bone is { } hitboxBone )
		{
			bone = smr.Model.Bones.GetBone( hitboxBone.Name );
		}
		else if ( smr.GetComponent<ModelPhysics>() is { } physics && physics.Model == smr.Model )
		{
			// Ragdolls are hit on their bodies' colliders
			foreach ( var body in physics.Bodies )
			{
				if ( body.Component.IsValid() && body.Component.GameObject == tr.GameObject )
					bone = smr.Model.Bones.AllBones[body.Bone];
			}
		}

		return bone;
	}

	static void ResizeBone( SkinnedModelRenderer smr, BoneCollection.Bone bone, float size )
	{
		smr.TryGetBoneOffset( bone, out var offset );

		var scale = (offset.UniformScale + size).Clamp( BoneEditSystem.MinScale, BoneEditSystem.MaxScale );
		BoneEditSystem.SetBoneOffset( smr, bone.Name, offset.WithScale( scale ) );
	}

	static void ResetBone( SkinnedModelRenderer smr, BoneCollection.Bone bone )
	{
		if ( !smr.TryGetBoneOffset( bone, out var offset ) ) return;

		// Only the size is ours, a bone moved or turned in the bone editor stays that way
		if ( offset.Position.IsNearlyZero() && Rotation.Difference( offset.Rotation, Rotation.Identity ).Angle() < 0.01f )
			BoneEditSystem.ClearBoneOffset( smr, bone.Name );
		else
			BoneEditSystem.SetBoneOffset( smr, bone.Name, offset.WithScale( 1.0f ) );
	}

	[Rpc.Broadcast]
	private void ResetScale( GameObject go )
	{
		if ( !go.IsValid() ) return;
		if ( go.IsProxy ) return;

		go.WorldScale = Vector3.One;
	}

	[Rpc.Broadcast]
	private void Resize( GameObject go, float size )
	{
		if ( !go.IsValid() ) return;
		if ( go.IsProxy ) return;

		var newScale = go.WorldScale + size;
		if ( newScale.Length < 0.1f ) return;
		if ( newScale.Length > 1000f ) return;

		var scale = Vector3.Max( newScale, 0.01f );
		go.WorldScale = scale;
	}
}
