using Sandbox.Rendering;
using Sandbox.Utility;

public class RpgWeapon : BaseWeapon
{
	[Property] public float TimeBetweenShots { get; set; } = 2f;
	[Property] public GameObject ProjectilePrefab { get; set; }
	[Property] public SoundEvent ShootSound { get; set; }

	[Sync( SyncFlags.FromHost )] RpgProjectile Projectile { get; set; }

	TimeSince TimeSinceShoot;

	protected override float GetPrimaryFireRate() => TimeBetweenShots;

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

			CreateProjectile( initialPos, transform.Forward, 1024 );

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
			CreateProjectile( muzzleTransform.Position, muzzleTransform.Rotation.Forward, 1024 );
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

		var gap = 6 + Easing.EaseOut( tss ) * 32;
		var len = 6;
		var w = 2;

		Color color = !CanPrimaryAttack() ? CrosshairNoShoot : CrosshairCanShoot;

		hud.SetBlendMode( BlendMode.Lighten );

		// Define the size of the square
		var squareSize = 64f;

		// Draw the four edges of the square
		hud.DrawLine( center + new Vector2( -squareSize / 2, -squareSize / 2 ), center + new Vector2( squareSize / 2, -squareSize / 2 ), w, color ); // Top edge
		hud.DrawLine( center + new Vector2( squareSize / 2, -squareSize / 2 ), center + new Vector2( squareSize / 2, squareSize / 2 ), w, color );   // Right edge
		hud.DrawLine( center + new Vector2( squareSize / 2, squareSize / 2 ), center + new Vector2( -squareSize / 2, squareSize / 2 ), w, color );  // Bottom edge
		hud.DrawLine( center + new Vector2( -squareSize / 2, squareSize / 2 ), center + new Vector2( -squareSize / 2, -squareSize / 2 ), w, color ); // Left edge

	}
}
