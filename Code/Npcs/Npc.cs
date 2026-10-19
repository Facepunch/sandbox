using Sandbox.Npcs.Layers;

namespace Sandbox.Npcs;

[Hide]
public partial class Npc : Component, IKillSource, Component.IDamageable
{
	[Property]
	public bool ShowDebugOverlay { get; set; }

	[Property]
	public SkinnedModelRenderer Renderer { get; set; }

	/// <summary>
	/// The name shown in the kill feed when this NPC is killed.
	/// </summary>
	[Property]
	public string DisplayName { get; set; } = "#spawnmenu.entity.npc";

	/// <summary>
	/// Current health. The NPC dies when this drops below 1.
	/// </summary>
	[Property, Title( "#entity.property.npc_health" ), Description( "#entity.description.npc_health" ), ClientEditable, Sync]
	public float Health { get; set; } = 100f;

	// IKillSource
	string IKillSource.DisplayName => DisplayName;
	string IKillSource.Tags => "npc";

	private Rigidbody _rigidbody;
	private NavMeshAgent _navAgent;
	[Sync( SyncFlags.FromHost )]
	private TimeSince _timeSincePhysicsEnabled { get; set; }

	protected override void OnStart()
	{
		GameObject.Tags.Add( "npc" );
		_rigidbody = GetComponent<Rigidbody>();
		_navAgent = GetComponent<NavMeshAgent>();
		Scene.GetSystem<NpcSystem>().Register( this );
	}

	protected override void OnDestroy()
	{
		Scene?.GetSystem<NpcSystem>()?.Unregister( this );
		base.OnDestroy();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		CategorizeGround();
		UpdateAirborne();

		// No thinking in mid-air - the schedule picks up again once we land
		if ( !IsAirborne )
			TickSchedule();

		if ( ShowDebugOverlay )
		{
			DrawDebugString();
		}
	}

	/// <summary>
	/// Spawns a networked ragdoll prop at the NPC's current position, copying the renderer and clothing,
	/// and optionally applies a launch velocity from the attacker. Host only.
	/// </summary>
	protected void CreateRagdoll( Vector3 velocity, Vector3 origin, float duration = 30 )
	{
		if ( IsProxy )
			return;

		if ( !Renderer.IsValid() || Renderer.Model is null )
			return;

		var go = new GameObject( false, "Ragdoll" );
		go.Tags.Add( "ragdoll" );
		go.Tags.Add( "removable" );
		go.WorldTransform = WorldTransform;

		var prop = go.AddComponent<Prop>();
		prop.Model = Renderer.Model;
		prop.BodyGroups = Renderer.BodyGroups;
		prop.MaterialGroup = Renderer.MaterialGroup;
		prop.Tint = Renderer.Tint;

		if ( Components.TryGet<Ownable>( out var ownable ) )
			Ownable.Set( go, ownable.Owner );

		// The prop builds its renderer and ModelPhysics when it's enabled, which the batch defers to its end
		using ( Scene.BatchGroup() )
		{
			go.Enabled = true;
		}

		var mainBody = go.GetComponent<SkinnedModelRenderer>();
		var physics = go.GetComponent<ModelPhysics>();

		if ( mainBody.IsValid() )
		{
			mainBody.CopyFrom( Renderer );
			mainBody.UseAnimGraph = false;

			// copy the clothes
			foreach ( var clothing in Renderer.GameObject.Children.SelectMany( x => x.Components.GetAll<SkinnedModelRenderer>() ) )
			{
				if ( !clothing.IsValid() ) continue;

				var newClothing = new GameObject( true, clothing.GameObject.Name );
				newClothing.Parent = go;

				var item = newClothing.Components.Create<SkinnedModelRenderer>();
				item.CopyFrom( clothing );
				item.BoneMergeTarget = mainBody;
			}
		}

		if ( physics.IsValid() )
		{
			physics.CopyBonesFrom( Renderer, true );
			ApplyRagdollForce( physics, velocity, origin );
		}

		go.NetworkSpawn( true, null );

		//
		// Destroy after a while
		//
		prop.Invoke( duration, prop.DestroyGameObject );
	}

	void ApplyRagdollForce( ModelPhysics physics, Vector3 force, Vector3 origin )
	{
		if ( !physics.IsValid() ) return;
		if ( force.Length < 1 ) return;

		foreach ( var body in physics.Bodies )
		{
			var rb = body.Component;
			if ( !rb.IsValid() ) continue;
			rb.ApplyImpulse( Vector3.Direction( origin, rb.WorldPosition ) * force.Length * rb.Mass );
		}
	}

	/// <summary>
	/// Resolves the attacker's current velocity from whatever movement source it has.
	/// </summary>
	protected Vector3 GetAttackerVelocity( GameObject attacker )
	{
		if ( !attacker.IsValid() )
			return Vector3.Zero;

		if ( attacker.GetComponent<Rigidbody>() is { } rb )
			return rb.Velocity;

		return Vector3.Zero;
	}

	/// <summary>
	/// How hard a melee kill knocks the body - a solid whack, not a launch.
	/// </summary>
	private const float MeleeKnockback = 200f;

	/// <summary>
	/// Calculates the launch velocity for a ragdoll based on the damage source.
	/// For explosions, uses the direction from the blast origin to this NPC. Bullets push along the
	/// shot by the weapon's ballistic force, melee gives a shove along the swing.
	/// Otherwise, falls back to the attacker's physical velocity.
	/// </summary>
	protected Vector3 GetDeathLaunchVelocity( in DamageInfo damage )
	{
		if ( damage.Tags.Contains( DamageTags.Explosion ) && damage.Origin != Vector3.Zero )
		{

			var dist = (WorldPosition - damage.Origin).Length;
			var strength = MathX.Remap( dist, 0, 512, 500, 1500 ).Clamp( 500, 1500 );

			var dir = (WorldPosition - damage.Origin).Normal;
			dir += Vector3.Up * 1.0f;
			dir = dir.Normal;

			return dir * strength;
		}

		// Only guns use their ballistics - melee weapons carry an unused ballistic config (the crowbar's
		// says 3000) that would throw the body across the map
		var weapon = damage.Weapon?.GetComponent<BaseCombatWeapon>();
		var force = weapon switch
		{
			MeleeWeapon => MeleeKnockback,
			BaseBulletWeapon bullets => bullets.Ballistics.Force,
			_ => 0f,
		};

		if ( force > 0 && damage.Position != damage.Origin )
		{
			return Vector3.Direction( damage.Origin, damage.Position ) * force;
		}

		return GetAttackerVelocity( damage.Attacker );
	}

	/// <summary>
	/// Base damage handling: subtract health, let the subclass react via <see cref="OnHurt"/>,
	/// and die at zero. Subclasses react by overriding OnHurt and Die -- they don't touch this.
	/// </summary>
	void IDamageable.OnDamage( in DamageInfo damage )
	{
		if ( IsProxy || Health < 1f )
			return;

		var amount = damage.Damage;

		// Same hitgroup rule as players - headshots hurt double.
		if ( damage.Tags.Contains( DamageTags.Headshot ) )
			amount *= 2f;

		Health -= amount;
		OnHurt( damage );

		if ( Health < 1f )
		{
			Speech.Stop();
			Die( damage );
		}
	}

	/// <summary>Called when the NPC takes damage (before the death check). Override to react.</summary>
	protected virtual void OnHurt( in DamageInfo damage ) { }

	/// <summary>
	/// Notifies the kill feed, spawns a ragdoll, and destroys this NPC. Called automatically
	/// when health drops below 1. Override to add NPC-specific behaviour before/after death.
	/// </summary>
	protected virtual void Die( in DamageInfo damage )
	{
		// Broadcast the death so nearby NPCs notice and react.
		EmitStimulus( StimulusKind.Death, radius: 1024f, lifetime: 2f );

		GameManager.Current?.OnNpcDeath( this, damage );
		CreateRagdoll( GetDeathLaunchVelocity( damage ), damage.Origin );
		GameObject.Destroy();
	}
}
