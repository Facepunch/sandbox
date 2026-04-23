using Sandbox.Rendering;
using Sandbox.Utility;

public class RpgWeapon : BaseWeapon
{
	[Property] public float TimeBetweenShots { get; set; } = 2f;
	[Property] public GameObject ProjectilePrefab { get; set; }
	[Property] public SoundEvent ShootSound { get; set; }
	[Property] public float ProjectileSpeed { get; set; } = 1024f;

	/// <summary>
	/// When enabled, fired rockets will continuously track toward the player's crosshair.
	/// Toggle with right-click (player) or SecondaryInput (standalone/seat).
	/// </summary>
	[Property, Sync, ClientEditable] public bool IsTrackedAim { get; set; } = false;

	[Sync( SyncFlags.FromHost )] RpgProjectile Projectile { get; set; }

	TimeSince TimeSinceShoot;

	/// <summary>
	/// Whether a live rocket is currently being guided toward the crosshair.
	/// </summary>
	public bool IsGuiding => IsTrackedAim && Projectile.IsValid();

	protected override float GetPrimaryFireRate() => TimeBetweenShots;

	public override bool CanSecondaryAttack() => false;

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		if ( Input.Pressed( "attack2" ) )
			ToggleTrackedAim();

		if ( IsGuiding )
		{
			var target = GetAimTarget( player.EyeTransform );
			Projectile.UpdateWithTarget( target, ProjectileSpeed );
		}
	}

	/// <summary>
	/// Standalone / seat control — uses SecondaryInput to toggle tracking.
	/// </summary>
	public override void OnControl()
	{
		base.OnControl();

		if ( HasOwner || IsProxy ) return;

		if ( SecondaryInput.Pressed() )
			ToggleTrackedAim();

		if ( IsGuiding )
		{
			var target = GetAimTarget( AimTransform );
			Projectile.UpdateWithTarget( target, ProjectileSpeed );
		}
	}

	[Rpc.Host]
	private void ToggleTrackedAim()
	{
		IsTrackedAim = !IsTrackedAim;
	}

	/// <summary>
	/// Aim source: player eye when held, muzzle/seat when standalone.
	/// </summary>
	private Transform AimTransform
	{
		get
		{
			var seated = ClientInput.Current;
			if ( seated.IsValid() )
			{
				var muzzlePos = MuzzleTransform.WorldTransform.Position;
				return new Transform( muzzlePos, seated.EyeTransform.Rotation );
			}

			return MuzzleTransform.WorldTransform;
		}
	}

	/// <summary>
	/// Traces from the given aim transform and returns the world-space point the player is looking at.
	/// </summary>
	private Vector3 GetAimTarget( Transform aim )
	{
		var tr = Scene.Trace.Ray( aim.Position, aim.Position + aim.Forward * 16384f )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.WithoutTags( "trigger", "projectile" )
			.Run();

		return tr.Hit ? tr.HitPosition : aim.Position + aim.Forward * 16384f;
	}

	public override void PrimaryAttack()
	{
		if ( HasOwner && !TakeAmmo( 1 ) )
		{
			TryAutoReload();
			return;
		}

		TimeSinceShoot = 0;
		AddShootDelay( TimeBetweenShots );

		if ( ViewModel.IsValid() )
			ViewModel.RunEvent<ViewModel>( x => x.OnAttack() );
		else if ( WorldModel.IsValid() )
			WorldModel.RunEvent<WorldModel>( x => x.OnAttack() );

		if ( ShootSound.IsValid() )
			GameObject.PlaySound( ShootSound );

		if ( HasOwner )
		{
			var transform = Owner.EyeTransform;
			transform.Position = transform.Position + Vector3.Down * 8f + transform.Right * 8f;
			var forward = transform.Forward;
			var initialPos = transform.ForwardRay.Position + (forward * 64.0f);

			initialPos = CheckThrowPosition( Owner, transform.Position, initialPos );

			CreateProjectile( initialPos, transform.Forward, ProjectileSpeed );

			Owner.Controller.EyeAngles += new Angles( Random.Shared.Float( -0.2f, -0.3f ), Random.Shared.Float( -0.1f, 0.1f ), 0 );

			if ( !Owner.Controller.ThirdPerson && Owner.IsLocalPlayer )
			{
				new Sandbox.CameraNoise.Punch( new Vector3( Random.Shared.Float( 45, 35 ), Random.Shared.Float( -10, -5 ), 0 ), 1.5f, 2, 0.5f );
				new Sandbox.CameraNoise.Shake( 1f, 0.6f );

				if ( HasAmmo() )
				{
					ViewModel?.RunEvent<ViewModel>( x => x.OnReloadStart() );
				}
			}
		}
		else
		{
			// Seat / standalone — fire straight from the muzzle
			var muzzleTransform = MuzzleTransform.WorldTransform;
			CreateProjectile( muzzleTransform.Position, muzzleTransform.Rotation.Forward, ProjectileSpeed );
		}
	}

	private Vector3 CheckThrowPosition( Player player, Vector3 eyePosition, Vector3 grenadePosition )
	{
		var tr = Scene.Trace.Box( BBox.FromPositionAndSize( Vector3.Zero, 8.0f ), eyePosition, grenadePosition )
			.WithoutTags( "trigger", "ragdoll", "player", "effect" )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.Run();

		if ( tr.Hit )
			return tr.EndPosition;

		return grenadePosition;
	}

	/// <summary>
	/// Creates the projectile with the host's permission
	/// </summary>
	[Rpc.Host]
	void CreateProjectile( Vector3 start, Vector3 direction, float speed )
	{
		var go = ProjectilePrefab?.Clone( start );

		var projectile = go.GetComponent<RpgProjectile>();
		Assert.True( projectile.IsValid(), "RpgProjectile not on projectile prefab" );

		if ( Owner.IsValid() )
			projectile.Instigator = Owner.PlayerData;

		go.NetworkSpawn();

		Projectile = projectile;
		projectile.UpdateDirection( direction, speed );
	}

	public override void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var tss = TimeSinceShoot.Relative.Remap( 0, 0.2f, 1, 0 );
		var w = 2;

		hud.SetBlendMode( BlendMode.Lighten );

		if ( IsTrackedAim )
		{
			// Diamond crosshair when in tracked aim mode
			Color guideColor = IsGuiding ? new Color( 1f, 0.5f, 0.1f ) : CrosshairCanShoot;
			var size = 32f;

			hud.DrawLine( center + new Vector2( 0, -size ), center + new Vector2( size, 0 ), w, guideColor );
			hud.DrawLine( center + new Vector2( size, 0 ), center + new Vector2( 0, size ), w, guideColor );
			hud.DrawLine( center + new Vector2( 0, size ), center + new Vector2( -size, 0 ), w, guideColor );
			hud.DrawLine( center + new Vector2( -size, 0 ), center + new Vector2( 0, -size ), w, guideColor );

			return;
		}

		Color color = !CanPrimaryAttack() ? CrosshairNoShoot : CrosshairCanShoot;

		var squareSize = 64f;

		hud.DrawLine( center + new Vector2( -squareSize / 2, -squareSize / 2 ), center + new Vector2( squareSize / 2, -squareSize / 2 ), w, color );
		hud.DrawLine( center + new Vector2( squareSize / 2, -squareSize / 2 ), center + new Vector2( squareSize / 2, squareSize / 2 ), w, color );
		hud.DrawLine( center + new Vector2( squareSize / 2, squareSize / 2 ), center + new Vector2( -squareSize / 2, squareSize / 2 ), w, color );
		hud.DrawLine( center + new Vector2( -squareSize / 2, squareSize / 2 ), center + new Vector2( -squareSize / 2, -squareSize / 2 ), w, color );
	}
}
