using System.Threading;

public sealed partial class ViewModel : WeaponModel, ICameraSetup
{
	[ConVar( "sbdm.hideviewmodel", ConVarFlags.Cheat )]
	private static bool HideViewModel { get; set; } = false;

	/// <summary>
	/// A sound to play at a specific time during reload.
	/// </summary>
	public record struct ReloadSoundEntry
	{
		/// <summary>
		/// Seconds after reload starts to play this sound.
		/// </summary>
		[KeyProperty] public float Time { get; set; }

		/// <summary>
		/// The sound to play.
		/// </summary>
		[Property, KeyProperty] public SoundEvent Sound { get; set; }
	}

	/// <summary>
	/// Timed sound events to play during reload.
	/// </summary>
	[Property, Group( "Reload Sounds" )]
	public List<ReloadSoundEntry> ReloadSoundEvents { get; set; } = new();

	/// <summary>
	/// Timed sound events to play during each incremental reload cycle.
	/// </summary>
	[Property, Group( "Reload Sounds" )]
	public List<ReloadSoundEntry> IncrementalReloadSoundEvents { get; set; } = new();

	/// <summary>
	/// Timed sound events played when starting an incremental reload sequence.
	/// </summary>
	[Property, Group( "Reload Sounds" )]
	public List<ReloadSoundEntry> IncrementalReloadStartSounds { get; set; } = new();

	/// <summary>
	/// Timed sound events played when finishing an incremental reload sequence.
	/// </summary>
	[Property, Group( "Reload Sounds" )]
	public List<ReloadSoundEntry> IncrementalReloadFinishSounds { get; set; } = new();

	private CancellationTokenSource _reloadSoundCts;
	private CancellationTokenSource _reloadFinishSoundCts;

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
	/// Use fast anims?
	/// </summary>
	[Property] 
	public bool UseFastAnimations { get; set; } = false;

	/// <summary>
	/// How much inertia should this weapon have?
	/// </summary>
	[Property, Group( "Inertia" )]
	Vector2 InertiaScale { get; set; } = new Vector2( 2, 2 );

	public bool IsAttacking { get; set; }

	TimeSince AttackDuration;

	bool _reloadFinishing;
	TimeSince _reloadFinishTimer;

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
		var rot = Scene.Camera.WorldRotation.Angles();

		// Need to fetch data from the camera for the first frame
		if ( isFirstUpdate )
		{


			lastInertia = new Vector2( rot.pitch, rot.yaw );
			currentInertia = Vector2.Zero;
			isFirstUpdate = false;
		}

		var newPitch = rot.pitch;
		var newYaw = rot.yaw;

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
			cc.WorldPosition += cc.WorldRotation * bone.Position * scale;
			cc.WorldRotation *= bone.Rotation * scale;
		}
	}

	void UpdateAnimation()
	{
		var playerController = GetComponentInParent<PlayerController>();
		if ( !playerController.IsValid() ) return;

		var rot = Scene.Camera.WorldRotation.Angles();

		Renderer.Set( "b_twohanded", true );
		Renderer.Set( "deploy_type", UseFastAnimations ? 1 : 0 );
		Renderer.Set( "reload_type", UseFastAnimations ? 1 : 0 );

		Renderer.Set( "b_grounded", playerController.IsOnGround );
		Renderer.Set( "move_bob", GamePreferences.ViewBobbing ? playerController.Velocity.Length.Remap( 0, playerController.RunSpeed * 2f ) : 0 );

		Renderer.Set( "aim_pitch", rot.pitch );
		Renderer.Set( "aim_pitch_inertia", currentInertia.x * InertiaScale.x );

		Renderer.Set( "aim_yaw", rot.yaw );
		Renderer.Set( "aim_yaw_inertia", currentInertia.y * InertiaScale.y );

		Renderer.Set( "attack_hold", IsAttacking ? AttackDuration.Relative.Clamp( 0f, 1f ) : 0f );

		if ( _reloadFinishing && _reloadFinishTimer >= 0.5f )
		{
			_reloadFinishing = false;
			Renderer.Set( "speed_reload", AnimationSpeed );
			Renderer.Set( "b_reloading", false );
		}

		var velocity = playerController.Velocity;

		var dir = velocity;
		var forward = Scene.Camera.WorldRotation.Forward.Dot( dir );
		var sideward = Scene.Camera.WorldRotation.Right.Dot( dir );

		var angle = MathF.Atan2( sideward, forward ).RadianToDegree().NormalizeDegrees();

		Renderer.Set( "move_direction", angle );
		Renderer.Set( "move_speed", velocity.Length );
		Renderer.Set( "move_groundspeed", velocity.WithZ( 0 ).Length );
		Renderer.Set( "move_y", sideward );
		Renderer.Set( "move_x", forward );
		Renderer.Set( "move_z", velocity.z );

	    var host = Game.ActiveScene.Get<SpawnMenuHost>();
		var mode = SpawnMenuHost.GetActiveMode()?.GetType().Name;
		if ( ( host?.Panel?.HasClass("open") ?? false ) && 
			( mode is "ContextMenuHost" or "SaveMenu" or "EffectsHost" ) )
		{
			var cursor = Sandbox.Mouse.Position;
			var screenRay = Scene.Camera.ScreenPixelToRay( cursor );
			var lookRot = Rotation.LookAt( screenRay.Forward );
			Renderer.WorldRotation = Rotation.Lerp( Renderer.WorldRotation, lookRot, Time.Delta * 5.0f );
		}
		else
			Renderer.LocalRotation = Rotation.Lerp( Renderer.LocalRotation, Angles.Zero, Time.Delta * 5.0f );
	}

	public override void OnAttack()
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

	public override void CreateRangedEffects( BaseWeapon weapon, Vector3 hitPoint, Vector3? origin )
	{
		DoTracerEffect( hitPoint, origin );
	}

	/// <summary>
	/// Called when starting to reload a weapon.
	/// </summary>
	public void OnReloadStart()
	{
		_reloadFinishing = false; // cancel any pending incremental finish from a previous reload
		Renderer?.Set( "speed_reload", AnimationSpeed );
		Renderer?.Set( IsIncremental ? "b_reloading" : "b_reload", true );

		if ( IsIncremental )
			StartSounds( IncrementalReloadStartSounds, ref _reloadFinishSoundCts );

		StartSounds( ReloadSoundEvents, ref _reloadSoundCts );
	}

	/// <summary>
	/// Called when incrementally reloading a weapon.
	/// </summary>
	public void OnIncrementalReload()
	{
		Renderer?.Set( "speed_reload", IncrementalAnimationSpeed );
		Renderer?.Set( "b_reloading_shell", true );

		StartSounds( IncrementalReloadSoundEvents, ref _reloadSoundCts );
	}

	public void OnReloadFinish()
	{
		CancelSounds( ref _reloadSoundCts );

		if ( IsIncremental )
		{
			StartSounds( IncrementalReloadFinishSounds, ref _reloadFinishSoundCts );

			_reloadFinishing = true;
			_reloadFinishTimer = 0;
		}
		else
		{
			Renderer?.Set( "b_reload", false );
		}
	}

	public void OnReloadCancel()
	{
		CancelSounds( ref _reloadSoundCts );
		CancelSounds( ref _reloadFinishSoundCts );
	}

	private void StartSounds( List<ReloadSoundEntry> events, ref CancellationTokenSource cts )
	{
		CancelSounds( ref cts );

		if ( events.Count == 0 )
			return;

		cts = new CancellationTokenSource();
		_ = PlaySoundsAsync( events, cts.Token );
	}

	private void CancelSounds( ref CancellationTokenSource cts )
	{
		if ( cts is null ) return;

		cts.Cancel();
		cts.Dispose();
		cts = null;
	}

	private async Task PlaySoundsAsync( List<ReloadSoundEntry> events, CancellationToken ct )
	{
		var sorted = events.OrderBy( e => e.Time ).ToList();
		var elapsed = 0f;

		foreach ( var entry in sorted )
		{
			var delay = entry.Time - elapsed;

			if ( delay > 0f )
				await Task.DelaySeconds( delay, ct );

			if ( ct.IsCancellationRequested )
				return;

			if ( entry.Sound is not null )
				GameObject.PlaySound( entry.Sound );

			elapsed = entry.Time;
		}
	}
}
