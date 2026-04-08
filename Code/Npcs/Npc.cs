using Sandbox.Npcs.Layers;

namespace Sandbox.Npcs;

[Hide]
public partial class Npc : Component, IKillSource
{
	[Property]
	public bool ShowDebugOverlay { get; set; }

	[Property]
	public SkinnedModelRenderer Renderer { get; set; }

	/// <summary>
	/// The name shown in the kill feed when this NPC is killed.
	/// </summary>
	[Property]
	public string DisplayName { get; set; } = "NPC";

	// IKillSource
	string IKillSource.DisplayName => DisplayName;
	string IKillSource.Tags => "npc";

	private Rigidbody _rigidbody;
	private NavMeshAgent _navAgent;
	private TimeSince _timeSincePhysicsEnabled;

	protected override void OnStart()
	{
		GameObject.Tags.Add( "npc" );
		_rigidbody = GetComponent<Rigidbody>();
		_navAgent = GetComponent<NavMeshAgent>();
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !_rigidbody.IsValid() || !_navAgent.IsValid() ) return;

		if ( _rigidbody.MotionEnabled )
		{
			// Physics is active (physgun grabbed us), so stop NavMesh from fighting the physics position.
			if ( _navAgent.UpdatePosition )
			{
				_navAgent.UpdatePosition = false;
				_timeSincePhysicsEnabled = 0;
			}

			// Once no longer constrained by a joint and velocity has settled, hand control back to navmesh
			var isJointHeld = _rigidbody.Joints.Count > 0;
			if ( !isJointHeld && _timeSincePhysicsEnabled > 0.5f && _rigidbody.Velocity.Length < 20f )
			{
				_rigidbody.MotionEnabled = false;
				_navAgent.Enabled = false;

				// Re-register the agent at the physics landing position by disabling and re-enabling it.
				_navAgent.Enabled = true;
				_navAgent.Stop();
				_navAgent.UpdatePosition = true;
			}
		}
		else if ( !_navAgent.UpdatePosition )
		{
			// MotionEnabled was cleared externally (eg. physgun), so re-enable NavMesh.
			_navAgent.UpdatePosition = true;
		}
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		TickSchedule();

		if ( ShowDebugOverlay )
		{
			DrawDebugString();
		}
	}

	/// <summary>
	/// Spawns a ragdoll at the NPC's current position, copying the renderer and clothing,
	/// and optionally applies a launch velocity from the attacker.
	/// </summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	protected void CreateRagdoll( Vector3 velocity, float duration = 30 )
	{
		if ( !Renderer.IsValid() )
			return;

		var go = new GameObject( true, "Ragdoll" );
		go.Tags.Add( "ragdoll" );
		go.WorldTransform = WorldTransform;

		var mainBody = go.Components.Create<SkinnedModelRenderer>();
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

		var physics = go.Components.Create<ModelPhysics>();
		physics.Model = mainBody.Model;
		physics.Renderer = mainBody;
		physics.CopyBonesFrom( Renderer, true );

		if ( velocity.LengthSquared > 0f )
		{
			foreach ( var body in physics.Bodies )
			{
				body.Component.Velocity = velocity;
			}
		}

		//
		// Destroy after a while
		//
		mainBody.Invoke( duration, mainBody.DestroyGameObject );
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
	/// Notifies the kill feed, spawns a ragdoll, and destroys this NPC.
	/// Call from subclass OnDamage when health drops below zero.
	/// Override to add NPC-specific behaviour before/after death.
	/// </summary>
	protected virtual void Die( in DamageInfo damage )
	{
		GameManager.Current?.OnNpcDeath( DisplayName, damage );
		CreateRagdoll( GetAttackerVelocity( damage.Attacker ) );
		GameObject.Destroy();
	}
}
