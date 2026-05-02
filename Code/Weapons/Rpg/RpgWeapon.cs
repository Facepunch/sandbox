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

	public override bool IsTargetedAim => IsTrackedAim;

	[Sync( SyncFlags.FromHost )] RpgProjectile Projectile { get; set; }

	TimeSince TimeSinceShoot;
	private bool _hasFired;
	private bool _waitingForReload;

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

		if ( _hasFired && Input.Released( "attack1" ) )
		{
			_hasFired = false;

			if ( HasAmmo() )
			{
				if ( IsGuiding )
					_waitingForReload = true;
				else
					ViewModel?.RunEvent<ViewModel>( x => x.OnReloadStart() );
			}
		}

		if ( IsGuiding )
		{
			var target = GetAimTarget();
			Projectile.UpdateWithTarget( target, ProjectileSpeed );
		}
		else if ( _waitingForReload && HasAmmo() )
		{
			_waitingForReload = false;
			ViewModel?.RunEvent<ViewModel>( x => x.OnReloadStart() );
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
			var target = GetAimTarget();
			Projectile.UpdateWithTarget( target, ProjectileSpeed );
		}
	}

	[Rpc.Host]
	private void ToggleTrackedAim()
	{
		IsTrackedAim = !IsTrackedAim;
	}

	/// <summary>
	/// Traces from AimRay and returns the world-space point the player is looking at.
	/// </summary>
	private Vector3 GetAimTarget()
	{
		var ray = AimRay;
		var tr = Scene.Trace.Ray( ray, 16384f )
			.IgnoreGameObjectHierarchy( AimIgnoreRoot )
			.WithoutTags( "trigger", "projectile" )
			.Run();

		return tr.Hit ? tr.HitPosition : ray.Position + ray.Forward * 16384f;
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

		var ray = AimRay;
		var muzzlePos = MuzzleTransform.WorldTransform.Position;
		var spawnPos = muzzlePos + ray.Forward * 64f;

		if ( HasOwner )
		{
			spawnPos = CheckThrowPosition( Owner, muzzlePos, spawnPos );

			Owner.Controller.EyeAngles += new Angles( Random.Shared.Float( -0.2f, -0.3f ), Random.Shared.Float( -0.1f, 0.1f ), 0 );

			if ( !Owner.Controller.ThirdPerson && Owner.IsLocalPlayer )
			{
				new Sandbox.CameraNoise.Punch( new Vector3( Random.Shared.Float( 45, 35 ), Random.Shared.Float( -10, -5 ), 0 ), 1.5f, 2, 0.5f );
				new Sandbox.CameraNoise.Shake( 1f, 0.6f );

				_hasFired = true;
			}
		}

		CreateProjectile( spawnPos, ray.Forward, ProjectileSpeed );
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
			projectile.Instigator = Owner;
		else if ( ClientInput.Current.IsValid() )
			projectile.Instigator = ClientInput.Current;

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
