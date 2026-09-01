using Sandbox.Rendering;

/// <summary>
/// M4 with an underbarrel grenade launcher on secondary fire. Grenades feed from the secondary
/// magazine (Clip2), topped back up from its reserve when reloading.
/// </summary>
[Title( "M4A1" )]
public sealed class M4a1Weapon : BaseBulletWeapon
{
	/// <summary>
	/// The grenade the launcher fires.
	/// </summary>
	[Property, Feature( "Grenade Launcher" )] public GameObject GrenadePrefab { get; set; }

	/// <summary>
	/// Launch speed of the grenade.
	/// </summary>
	[Property, Feature( "Grenade Launcher" )] public float GrenadeSpeed { get; set; } = 1500f;

	/// <summary>
	/// Reserve grenades granted on first pickup - reloads top the launcher's magazine up from these.
	/// </summary>
	[Property, Feature( "Grenade Launcher" )] public int StartingGrenades { get; set; } = 6;

	protected override void OnAdded( Sandbox.BaseInventoryComponent inventory )
	{
		// Seed the grenade reserve once, like the bullet reserve.
		if ( StartingGrenades > 0 && SecondaryAmmoType is not null && !inventory.HasAmmo( SecondaryAmmoType ) )
			inventory.GiveAmmo( SecondaryAmmoType, StartingGrenades );

		base.OnAdded( inventory );
	}

	public override void SecondaryAttack()
	{
		if ( !TakeSecondaryAmmo() )
			return;

		ShootEffects();

		// The attack runs once on the owner - LaunchGrenade is a host RPC, so the grenade spawns
		// exactly once, on the host.
		LaunchGrenade( GetMuzzleTransform().Position + AimRay.Forward * 16f, AimRay.Forward );
	}

	/// <summary>
	/// Spawns and launches the grenade with the host's permission.
	/// </summary>
	[Rpc.Host]
	void LaunchGrenade( Vector3 start, Vector3 direction )
	{
		if ( !GrenadePrefab.IsValid() )
			return;

		var go = GrenadePrefab.Clone( start );

		var explosive = go.GetOrAddComponent<TimedExplosive>();
		explosive.Attacker = Attacker;

		if ( go.GetComponent<Rigidbody>() is { } rb )
			rb.Velocity = direction * GrenadeSpeed + Vector3.Up * 100f;

		// Don't collide with the weapon it launched from
		var filter = go.AddComponent<PhysicsFilter>();
		filter.Body = GameObject;

		go.NetworkSpawn();
	}

	public override void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var gap = 16 + SpreadBloom * 32;
		var len = 12;
		var w = 2f;

		var color = !HasPrimaryAmmo() || IsReloading || NextPrimaryFire > 0 ? CrosshairNoShoot : CrosshairCanShoot;

		hud.SetBlendMode( BlendMode.Lighten );
		hud.DrawLine( center + Vector2.Left * (len + gap), center + Vector2.Left * gap, w, color );
		hud.DrawLine( center - Vector2.Left * (len + gap), center - Vector2.Left * gap, w, color );
		hud.DrawLine( center + Vector2.Up * (len + gap), center + Vector2.Up * gap, w, color );
		hud.DrawLine( center - Vector2.Up * (len + gap), center - Vector2.Up * gap, w, color );
	}
}
