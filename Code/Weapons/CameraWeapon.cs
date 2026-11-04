/// <summary>
/// A handheld camera. CameraFeed provides its video output; this component handles weapon controls.
/// </summary>
[Title( "#weapon.name.camera" ), Icon( "videocam" )]
public sealed class CameraWeapon : BaseSandboxWeapon
{
	public CameraWeapon()
	{
		// The camera doesn't use ammo (the engine default is on, which would show an ammo indicator).
		UsesAmmo = false;
	}

	float fov = 50;
	float roll = 0;

	DepthOfField dof;
	bool focusing;
	Vector3 focusPoint;

	[RequireComponent, Hide] public CameraFeed Feed { get; set; }
	[Property, Feature( "WorldModel" )] public Sandbox.Animation.Clip HoldAnimation { get; set; }

	Model _poseModel;
	Sandbox.Animation.Clip _poseClip;
	(BoneCollection.Bone Bone, Sandbox.Animation.Skeleton.Bone Track)[] _poseBones;
	Transform[] _poseTransforms;

	public override bool WantsHideHud => true;

	public override HudElement HiddenHudElements => HudElement.All & ~HudElement.Inventory;

	protected override void OnDisabled()
	{
		base.OnDisabled();

		DestroyDepthOfField();
	}

	protected override void OnDestroy()
	{
		DestroyDepthOfField();
	}

	protected override void OnPreRender()
	{
		if ( !Feed.IsValid() ) return;

		if ( HasOwner && Scene.Camera.IsValid() )
		{
			// When held, mirror the player's camera so the TV shows their POV.
			// TODO: network some props to the TV so they show up in the RT camera when held by a player other than the host.
			Feed.SetWorldView( Scene.Camera.View.Transform, Scene.Camera.View.FieldOfView, Scene.Camera.FovAxis, hideViewer: true );
		}
		else
		{
			Feed.UseLensView( 40f );
		}
	}

	/// <summary>
	/// We want to control the camera fov when held by a player.
	/// </summary>
	public override void OnCameraSetup( Player player, Sandbox.CameraComponent camera )
	{
		if ( !player.Network.IsOwner || !Network.IsOwner ) return;

		camera.FieldOfView = fov;
		camera.WorldRotation = camera.WorldRotation * new Angles( 0, 0, roll );
	}

	public override void OnCameraMove( Player player, ref Angles angles )
	{
		if ( Input.Down( "attack2" ) )
		{
			angles = default;
		}

		float sensitivity = fov.Remap( 1, 70, 0.01f, 1 );
		angles *= sensitivity;
	}

	protected override void OnControl()
	{
		if ( Input.Pressed( "reload" ) )
		{
			fov = 50;
			roll = 0;
		}

		if ( Input.Down( "attack2" ) )
		{
			fov += Input.AnalogLook.pitch;
			fov = fov.Clamp( 1, 150 );
			roll -= Input.AnalogLook.yaw;
		}

		EnsureDepthOfField();

		if ( dof.IsValid() )
		{
			UpdateDepthOfField( dof );
		}

		if ( focusing && Input.Released( "attack1" ) )
		{
			Game.TakeScreenshot();
			Sandbox.Services.Stats.Increment( "photos", 1 );

			PlayAttackSound();
		}

		focusing = Input.Down( "attack1" );
	}

	private void EnsureDepthOfField()
	{
		if ( dof.IsValid() ) return;

		dof = Scene.Camera.GetOrAddComponent<DepthOfField>();
		dof.Flags |= ComponentFlags.NotNetworked;
		focusing = false;
	}

	private void DestroyDepthOfField()
	{
		dof?.Destroy();
		dof = default;
	}

	private void UpdateDepthOfField( DepthOfField dof )
	{
		if ( !focusing )
		{
			dof.BlurSize = MathF.Pow( Scene.Camera.FieldOfView.Remap( 1, 55, 1, 0 ), 4 ) * 16;
			dof.FocusRange = 512;
			dof.FrontBlur = false;

			var tr = Scene.Trace.Ray( Scene.Camera.Transform.World.ForwardRay, 5000 )
								.Radius( 4 )
								.IgnoreGameObjectHierarchy( GameObject.Root )
								.Run();

			focusPoint = tr.EndPosition;
		}

		var target = Scene.Camera.WorldPosition.Distance( focusPoint ) + 64;

		dof.FocalDistance = dof.FocalDistance.LerpTo( target, Time.Delta * 2.0f );
	}

	/// <summary>
	/// Keeps the camera view clear of weapon HUD elements.
	/// </summary>
	public override void DrawHud( Painter painter, Vector2 crosshair )
	{
		// nothing!
	}

	internal void UpdateHoldAnimation( bool prepare )
	{
		var controller = Owner?.Controller;
		if ( !IsActive || !controller.IsValid() || !controller.Renderer.IsValid() || !WorldModel.IsValid() || HoldAnimation?.IsBound != true ) return;
		var body = controller.Renderer;
		var model = body.SceneModel;
		if ( !model.IsValid() ) return;
		if ( prepare )
		{
			// The clip supplies pitch; suppress the base graph's extra aiming bend.
			if ( body.Animation.Graph is not null ) body.Animation.Parameters.TrySet( "aim_body_weight", 0f );
#pragma warning disable CS0618
			else body.Set( "aim_body_weight", 0f );
#pragma warning restore CS0618
			return;
		}
		if ( _poseModel != body.Model || _poseClip != HoldAnimation || _poseBones is null )
		{
			var bones = new List<(BoneCollection.Bone Bone, Sandbox.Animation.Skeleton.Bone Track)>();
			// Clip bones are parent-first. Including descendants here also establishes
			// evaluation order, without sorting or walking ancestors each frame.
			foreach ( var track in HoldAnimation.Skeleton.Bones )
				if ( track.Name == "spine_0" || bones.Any( b => b.Track.Index == track.ParentIndex ) )
					if ( body.Model.Bones.GetBone( track.Name ) is { } bone ) bones.Add( (bone, track) );
			_poseBones = bones.ToArray();
			_poseTransforms = new Transform[HoldAnimation.Skeleton.BoneCount];
			_poseModel = body.Model;
			_poseClip = HoldAnimation;
		}

		// The baked clip is an aim strip: -85 degrees at the start, neutral in the
		// middle, +85 at the end. Leave pelvis/legs on the normal locomotion graph.
		var aim = (body.WorldRotation.Inverse * controller.EyeTransform.Rotation).Angles();
		var cycle = (aim.pitch.Clamp( -85f, 85f ) + 85f) / 170f;
		var yaw = Rotation.FromAxis( body.WorldRotation.Up, aim.yaw );
		foreach ( var (bone, track) in _poseBones )
		{
			bool root = track.Name == "spine_0";
			var parentWorld = root ? model.GetBoneWorldTransform( bone.Parent.Index ) : _poseTransforms[track.ParentIndex];
			var world = parentWorld.ToWorld( HoldAnimation.GetLocalTransform( cycle, track.Index ) );
			if ( root ) world.Rotation = yaw * world.Rotation;
			_poseTransforms[track.Index] = world;
			model.SetBoneWorldTransform( bone.Index, world );
			var go = body.GetBoneObject( bone.Index );
			if ( go.IsValid() ) go.WorldTransform = world;
		}
		foreach ( var clothing in body.GameObject.Components.GetAll<SkinnedModelRenderer>( FindMode.InDescendants ) )
			if ( clothing.BoneMergeTarget.IsValid() && clothing.SceneModel.IsValid() && clothing.BoneMergeTarget.SceneModel.IsValid() )
				clothing.SceneModel.MergeBones( clothing.BoneMergeTarget.SceneModel );
	}
}

public sealed class CameraHoldAnimationSystem : GameObjectSystem<CameraHoldAnimationSystem>
{
	public CameraHoldAnimationSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.UpdateBones, -500, PreparePoses, nameof( PreparePoses ) );
		Listen( Stage.UpdateBones, 10, UpdatePoses, nameof( UpdatePoses ) );
	}

	void PreparePoses()
	{
		if ( Scene.IsEditor ) return;
		foreach ( var camera in Scene.GetAll<CameraWeapon>() ) camera.UpdateHoldAnimation( prepare: true );
	}

	void UpdatePoses()
	{
		if ( Scene.IsEditor ) return;
		foreach ( var camera in Scene.GetAll<CameraWeapon>() ) camera.UpdateHoldAnimation( prepare: false );
	}
}
