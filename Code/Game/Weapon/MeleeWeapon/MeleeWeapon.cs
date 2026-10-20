public class MeleeWeapon : BaseSandboxWeapon
{
	/// <summary>
	/// Cooldown after a hit connects.
	/// </summary>
	[Property] public float SwingDelay { get; set; } = 0.5f;

	/// <summary>
	/// Cooldown after a swing misses.
	/// </summary>
	[Property] public float MissSwingDelay { get; set; } = 0.75f;

	/// <summary>
	/// Damage dealt per hit.
	/// </summary>
	[Property] public float Damage { get; set; } = 12f;

	/// <summary>
	/// Reach of the swing trace.
	/// </summary>
	[Property] public float Range { get; set; } = 128f;

	/// <summary>
	/// Radius of the swing trace sphere.
	/// </summary>
	[Property] public float SwingRadius { get; set; } = 10f;

	/// <summary>
	/// Physics impulse magnitude applied to hit objects.
	/// </summary>
	[Property] public float SwingForce { get; set; } = 1000f;

	[Property] public SoundEvent HitSound { get; set; }

	/// <summary>Random pitch of the first-person view punch per swing (min, max degrees).</summary>
	[Property, Group( "Camera" )] public Vector2 PunchPitch { get; set; } = new( -15f, -10f );

	/// <summary>Random yaw of the first-person view punch per swing (min, max degrees).</summary>
	[Property, Group( "Camera" )] public Vector2 PunchYaw { get; set; } = new( -10f, 0f );

	/// <summary>How many times the view punch oscillates before it lapses.</summary>
	[Property, Group( "Camera" )] public float PunchFrequency { get; set; } = 0.25f;

	/// <summary>How long the view punch lasts, in seconds.</summary>
	[Property, Group( "Camera" )] public float PunchDuration { get; set; } = 1f;

	/// <summary>First-person camera shake strength per swing, in world units.</summary>
	[Property, Group( "Camera" )] public float ShakeAmplitude { get; set; } = 1f;

	/// <summary>New shake offsets per second.</summary>
	[Property, Group( "Camera" )] public float ShakeFrequency { get; set; } = 40f;

	/// <summary>How long the shake takes to settle, in seconds.</summary>
	[Property, Group( "Camera" )] public float ShakeDuration { get; set; } = 1.2f;

	public MeleeWeapon()
	{
		// Melee doesn't use ammo (the engine default is on, which would leave us dry-firing an empty clip).
		UsesAmmo = false;
	}

	public bool CanAttack() => NextPrimaryFire <= 0;

	public override bool CanSecondaryAttack() => false;

	/// <summary>
	/// The swing. Runs once on the owning client - our trace decides what we hit, the engine claims it
	/// to the host, and the host applies the damage.
	/// </summary>
	public override void PrimaryAttack()
	{
		var forward = AimRay.Forward;

		var trace = Scene.Trace.Ray( AimRay with { Forward = forward }, Range )
							.IgnoreGameObjectHierarchy( AimIgnoreRoot )
							.WithoutTags( "playercontroller" )
							.UseHitboxes();

		var tr = trace.Run();
		if ( !tr.Hit )
		{
			tr = trace.Radius( SwingRadius ).Run();
		}

		// Hit and miss recover at different speeds - replace the flat cooldown FirePrimary set.
		SetNextPrimaryFire( tr.GameObject.IsValid() ? SwingDelay : MissSwingDelay );

		// Swing presentation - predicted on the owner, relayed to everyone else by the host.
		ShootEffects( new ShotEffect( tr.HitPosition, tr.Hit, tr.Normal, tr.GameObject, tr.Surface ) );

		// Our trace decides the hit - the engine claims it to the host, which applies the damage.
		// Melee doesn't count hitgroups - no headshot tags on the damage.
		ShootBullet( tr, Damage, SwingForce, hitboxTags: false );

		if ( !HasOwner )
			return;

		Owner.Controller.EyeAngles += new Angles( Random.Shared.Float( -0.2f, -0.3f ), Random.Shared.Float( -0.1f, 0.1f ), 0 );

		if ( !Owner.Controller.ThirdPerson && Owner.IsLocalPlayer )
		{
			// The swing kicks the view down-left and bounces back, with a little rumble on top.
			Scene.Camera?.AddPunch( new Angles( Random.Shared.Float( PunchPitch.x, PunchPitch.y ), Random.Shared.Float( PunchYaw.x, PunchYaw.y ), 0 ), PunchFrequency, PunchDuration );
			Scene.Camera?.AddShake( ShakeAmplitude, ShakeFrequency, ShakeDuration );
		}
	}

	protected override void OnShootEffects( ShotEffect shot )
	{
		if ( Application.IsDedicatedServer ) return;

		// Swing sound, holder anim, the model's attack anim and the impact come from the base.
		base.OnShootEffects( shot );

		if ( shot.Hit && shot.HitObject.IsValid() && ViewModel.IsValid() )
			ViewModel.RunEvent<ViewModel>( x => x.Renderer.Set( "b_attack_has_hit", true ) );
	}

	protected override void OnShootImpact( in ShotEffect shot )
	{
		if ( !shot.Hit || !shot.HitObject.IsValid() )
			return;

		// A melee thunk instead of the surface's bullet ricochet, so the impact prefab is spawned soundless.
		shot.HitObject.PlaySound(
			shot.Surface.SoundCollection.ImpactHard ?? shot.Surface.GetBaseSurface()?.SoundCollection.ImpactHard ?? HitSound,
			shot.HitObject.WorldTransform.PointToLocal( shot.HitPosition ) );

		ImpactPrefab( shot.HitObject, shot.Surface, shot.HitPosition, shot.Normal, GetShotDirection( shot ) );
	}

	/// <summary>
	/// Draws the aiming dot with the current attack availability.
	/// </summary>
	public override void DrawCrosshair( Painter painter, Vector2 center )
	{
		using var scope = painter.Scope();
		Color color = CanAttack() ? Color.White : Color.Red;

		painter.BlendMode = BlendMode.Lighten;
		painter.Fill = color;
		painter.Stroke = Stroke.None;
		painter.Circle( center, 3f );
	}
}
