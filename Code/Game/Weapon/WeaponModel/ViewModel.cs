using static BaseWeapon;

public sealed partial class ViewModel : WeaponModel, IWeaponEvent, ICameraSetup
{
	[ConVar( "sbdm.hideviewmodel", ConVarFlags.Cheat )]
	private static bool HideViewModel { get; set; } = false;

	/// <summary>
	/// Turns on incremental reloading parameters.
	/// </summary>
	[Property, Group( "Animation" )]
	public bool IsIncremental { get; set; } = false;

	/// <summary>
	/// Animation speed in general.
	/// </summary>
	[Property, Group( "Animation" )]
	public float AnimationSpeed { get; set; } = 1.0f;

	/// <summary>
	/// Animation speed for incremental reload sections.
	/// </summary>
	[Property, Group( "Animation" )]
	public float IncrementalAnimationSpeed { get; set; } = 3.0f;

	/// <summary>
	/// How much inertia should this weapon have?
	/// </summary>
	[Property, Group( "Inertia" )]
	Vector2 InertiaScale { get; set; } = new Vector2( 2, 2 );

	bool IsAttacking;
	TimeSince AttackDuration;

	Vector2 lastInertia;
	Vector2 currentInertia;
	bool isFirstUpdate = true;

	protected override void OnStart()
	{
		foreach ( var renderer in GetComponentsInChildren<ModelRenderer>() )
		{
			// Don't render shadows for viewmodels
			renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
		}
	}

	protected override void OnUpdate()
	{
		UpdateAnimation();
	}

	void ApplyInertia()
	{
		// Need to fetch data from the camera for the first frame
		if ( isFirstUpdate )
		{
			var rot = Scene.Camera.WorldRotation;

			lastInertia = new Vector2( rot.Pitch(), rot.Yaw() );
			currentInertia = Vector2.Zero;
			isFirstUpdate = false;
		}

		var newPitch = Scene.Camera.WorldRotation.Pitch();
		var newYaw = Scene.Camera.WorldRotation.Yaw();

		currentInertia = new Vector2( Angles.NormalizeAngle( newPitch - lastInertia.x ), Angles.NormalizeAngle( lastInertia.y - newYaw ) );
		lastInertia = new( newPitch, newYaw );
	}

	void ICameraSetup.Setup( CameraComponent cc )
	{
		Renderer.Enabled = !HideViewModel;

		WorldPosition = cc.WorldPosition;
		WorldRotation = cc.WorldRotation;

		ApplyInertia();
		ApplyAnimationTransform( cc );
	}

	void ApplyAnimationTransform( CameraComponent cc )
	{
		if ( !Renderer.IsValid() ) return;

		if ( Renderer.TryGetBoneTransformLocal( "camera", out var bone ) )
		{
			var scale = 0.5f;
			cc.LocalPosition += bone.Position * scale;
			cc.LocalRotation *= bone.Rotation * scale;
		}
	}

	void UpdateAnimation()
	{
		var playerController = GetComponentInParent<PlayerController>();
		if ( !playerController.IsValid() ) return;

		Renderer.Set( "b_twohanded", true );
		Renderer.Set( "b_grounded", playerController.IsOnGround );
		Renderer.Set( "move_bob", GamePreferences.ViewBobbing ? playerController.Velocity.Length.Remap( 0, playerController.RunSpeed * 2f ) : 0 );

		Renderer.Set( "aim_pitch_inertia", currentInertia.x * InertiaScale.x );
		Renderer.Set( "aim_yaw_inertia", currentInertia.y * InertiaScale.y );

		Renderer.Set( "attack_hold", IsAttacking ? AttackDuration.Relative.Clamp( 0f, 1f ) : 0f );
	}

	void IWeaponEvent.OnAttack( IWeaponEvent.AttackEvent e )
	{
		Renderer?.Set( "b_attack", true );

		DoMuzzleEffect();
		DoEjectBrass();

		if ( IsThrowable )
		{
			Renderer?.Set( "b_throw", true );

			Invoke( 0.5f, () =>
			{
				Renderer?.Set( "b_deploy_new", true );
				Renderer?.Set( "b_pull", false );
			} );
		}
	}

	void IWeaponEvent.CreateRangedEffects( BaseWeapon weapon, Vector3 hitPoint, Vector3? origin )
	{
		DoTracerEffect( hitPoint, origin );
	}

	/// <summary>
	/// Called when starting to reload a weapon.
	/// </summary>
	void IWeaponEvent.OnReloadStart()
	{
		Renderer?.Set( "speed_reload", AnimationSpeed );
		Renderer?.Set( IsIncremental ? "b_reloading" : "b_reload", true );
	}

	/// <summary>
	/// Called when incrementally reloading a weapon.
	/// </summary>
	void IWeaponEvent.OnIncrementalReload()
	{
		Renderer?.Set( "speed_reload", IncrementalAnimationSpeed );
		Renderer?.Set( "b_reloading_shell", true );
	}

	void IWeaponEvent.OnReloadFinish()
	{
		if ( IsIncremental )
		{
			//
			// Stops the reload after a little delay so it's not immediately cancelling the animation.
			//
			Invoke( 0.5f, () =>
			{
				Renderer?.Set( "speed_reload", AnimationSpeed );
				Renderer?.Set( "b_reloading", false );
			} );
		}
		else
		{
			Renderer?.Set( "b_reload", false );
		}
	}
}
