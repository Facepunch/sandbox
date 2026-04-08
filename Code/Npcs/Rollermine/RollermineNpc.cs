using Sandbox.Npcs.Layers;
using Sandbox.Npcs.Rollermine.Schedules;

namespace Sandbox.Npcs.Rollermine;

/// <summary>
/// A physics-driven NPC that chases players, leaps at them, and bounces off dealing damage on contact.
/// </summary>
public class RollermineNpc : Npc, Component.IDamageable, Component.ICollisionListener
{
	[Property, ClientEditable, Range( 1f, 500f ), Sync]
	public float Health { get; set; } = 35f;

	/// <summary>
	/// Continuous force applied per-frame while rolling toward a target.
	/// </summary>
	[Property, Group( "Balance" )]
	public float RollForce { get; set; } = 80000f;

	/// <summary>
	/// Torque applied per-frame to spin the sphere visually.
	/// </summary>
	[Property, Group( "Balance" )]
	public float RollTorque { get; set; } = 40000f;

	/// <summary>
	/// Upward impulse applied when the rollermine gets stuck.
	/// </summary>
	[Property, Group( "Balance" )]
	public float StuckJumpForce { get; set; } = 500f;

	/// <summary>
	/// Impulse applied when leaping at the target.
	/// </summary>
	[Property, Group( "Balance" )]
	public float LeapForce { get; set; } = 60000f;

	/// <summary>
	/// Upward component added to the leap direction (0 = flat, 1 = 45°).
	/// </summary>
	[Property, Group( "Balance" )]
	public float LeapUpwardBias { get; set; } = 0.2f;

	/// <summary>
	/// Impulse magnitude applied to self when bouncing off a surface/player.
	/// </summary>
	[Property, Group( "Balance" )]
	public float BounceForce { get; set; } = 450f;

	/// <summary>
	/// Damage applied to anything we crash into.
	/// </summary>
	[Property, Group( "Balance" )]
	public float ContactDamage { get; set; } = 20f;

	/// <summary>
	/// Distance at which we switch from rolling to leaping.
	/// </summary>
	[Property, Group( "Balance" )]
	public float LeapRange { get; set; } = 160f;

	/// <summary>
	/// Eye child GameObject — assign in editor.
	/// Rotated to face the current target each frame.
	/// </summary>
	[Property]
	public GameObject Eye { get; set; }

	public Rigidbody Rigidbody { get; private set; }

	private TimeSince _lastBounce;
	private const float BounceCooldown = 0.25f;

	protected override void OnStart()
	{
		base.OnStart();
		Rigidbody = GetComponent<Rigidbody>();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		TrackEye();
	}

	public override ScheduleBase GetSchedule()
	{
		var target = Senses.GetNearestVisible();
		if ( target.IsValid() )
			return GetSchedule<RollermineChaseSchedule>();

		return GetSchedule<RollermineIdleSchedule>();
	}

	void IDamageable.OnDamage( in DamageInfo damage )
	{
		if ( IsProxy ) return;

		Health -= damage.Damage;

		if ( Health <= 0f )
			Die( damage );
	}

	protected override void Die( in DamageInfo damage )
	{
		GameManager.Current?.OnNpcDeath( DisplayName, damage );

		// TODO: explosion effect / sound

		GameObject.Destroy();
	}

	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( IsProxy ) return;
		if ( !Rigidbody.IsValid() ) return;
		if ( _lastBounce < BounceCooldown ) return;

		var root = collision.Other.GameObject?.Root;
		if ( !root.IsValid() ) return;

		// Only react to damageable targets (players, NPCs) — not terrain/props
		if ( !root.Components.TryGet( out IDamageable damageable ) )
			return;

		_lastBounce = 0f;

		damageable.OnDamage( new DamageInfo
		{
			Damage = ContactDamage,
			Attacker = GameObject,
			Position = collision.Contact.Point,
		} );

		// Bounce up and away from the player rather than a pure reflection
		var away = (WorldPosition - root.WorldPosition).WithZ( 0 );
		if ( away.LengthSquared < 0.01f )
			away = WorldRotation.Backward.WithZ( 0 );

		var bounceDir = (away.Normal + Vector3.Up * 2f).Normal;
		Rigidbody.Velocity = Vector3.Zero;
		Rigidbody.ApplyImpulse( bounceDir * BounceForce );
	}

	private void TrackEye()
	{
		if ( !Eye.IsValid() ) return;

		var target = Senses.GetNearestVisible();
		if ( !target.IsValid() ) return;

		var dir = (target.WorldPosition - Eye.WorldPosition).Normal;
		if ( dir.LengthSquared < 0.01f ) return;

		Eye.WorldRotation = Rotation.LookAt( dir, Vector3.Up );
	}
}
