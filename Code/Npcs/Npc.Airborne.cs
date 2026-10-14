namespace Sandbox.Npcs;

public partial class Npc : Component.ICollisionListener
{
	/// <summary>
	/// Hitting something at or below this speed while airborne doesn't hurt - landing from a throw or
	/// a fall, or being slammed into a wall.
	/// </summary>
	[Property, Group( "Impact Damage" )]
	public float MaxSafeImpactSpeed { get; set; } = 350f;

	/// <summary>
	/// Hitting something at or above this speed is fatal.
	/// </summary>
	[Property, Group( "Impact Damage" )]
	public float FatalImpactSpeed { get; set; } = 1100f;

	/// <summary>
	/// Off our feet - thrown, dropped, or dangling from a physgun. Only NPCs that walk on the navmesh;
	/// ones that roll around on physics (rollermines) are never "airborne".
	/// </summary>
	public bool IsAirborne => _navAgent.IsValid() && !IsOnGround;

	private TimeSince _timeSinceImpact;
	private bool _wasAirborne;

	// Our velocity last frame in the air - by the time we land or hit something, it's already slowed us
	private Vector3 _airVelocity;

	/// <summary>
	/// While airborne the animation falls with the body - the animgraph's in-air pose, driven by the
	/// rigidbody rather than the (disabled) nav agent.
	/// </summary>
	private void UpdateAirborne()
	{
		var airborne = IsAirborne;

		// The ground check catches us a few units up, a frame before the body touches down - so a
		// landing often never reports a collision. Take the hit here, from how fast we were falling.
		if ( _wasAirborne && !airborne )
			TakeImpact( MathF.Max( -_airVelocity.z, 0f ), WorldPosition, DamageTags.Fall );

		_wasAirborne = airborne;
		Animation.Grounded = !airborne;

		if ( !airborne || !_rigidbody.IsValid() )
			return;

		_airVelocity = _rigidbody.Velocity;
		Animation.SetMove( _airVelocity, WorldRotation );
	}

	/// <summary>
	/// Hitting something hard while airborne - a wall we've been thrown into, or the floor if the
	/// collision beats the ground check to it. Only the speed straight into the surface counts, so
	/// skidding along it doesn't.
	/// </summary>
	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( IsProxy || !IsAirborne )
			return;

		var normal = collision.Contact.Normal;
		var speed = MathF.Max( MathF.Abs( collision.Contact.NormalSpeed ), MathF.Abs( Vector3.Dot( _airVelocity, normal ) ) );

		TakeImpact( speed, collision.Contact.Point, MathF.Abs( normal.z ) > 0.7f ? DamageTags.Fall : DamageTags.Impact );
	}

	/// <summary>
	/// A hard hit hurts - harsher than a player's fall damage, being flung around by a physgun
	/// should hurt. Nothing at or below <see cref="MaxSafeImpactSpeed"/>, fatal at <see cref="FatalImpactSpeed"/>.
	/// </summary>
	private void TakeImpact( float speed, Vector3 position, string tag )
	{
		if ( Health < 1f || speed <= MaxSafeImpactSpeed || FatalImpactSpeed <= MaxSafeImpactSpeed )
			return;

		// One hit, however many ways it gets reported
		if ( _timeSinceImpact < 0.25f )
			return;

		var amount = MathX.Remap( speed, MaxSafeImpactSpeed, FatalImpactSpeed, 0f, 100f );
		if ( amount < 1f )
			return;

		_timeSinceImpact = 0;

		var damage = new DamageInfo( amount, null, null ) { Position = position };
		damage.Tags.Add( tag );
		(this as IDamageable).OnDamage( damage );
	}
}
