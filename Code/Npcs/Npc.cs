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
	/// Spawns a ragdoll at the NPC's current position, copying the renderer and clothing.
	/// Automatically destroyed after <paramref name="duration"/> seconds.
	/// </summary>
	protected void CreateRagdoll( float duration = 30f )
	{
		if ( !Renderer.IsValid() )
			return;

		using var batch = Scene.BatchGroup();

		var go = new GameObject( false, "RagdollSpawner" );
		go.WorldTransform = WorldTransform;

		var spawner = go.AddComponent<RagdollSpawner>();
		spawner.Renderer = Renderer;

		go.NetworkSpawn();
		spawner.Invoke( duration, spawner.DestroyGameObject );
	}

	/// <summary>
	/// Notifies the kill feed, spawns a ragdoll, and destroys this NPC.
	/// Call from subclass OnDamage when health drops below zero.
	/// Override to add NPC-specific behaviour before/after death.
	/// </summary>
	protected virtual void Die( in DamageInfo damage )
	{
		GameManager.Current?.OnNpcDeath( DisplayName, damage );
		CreateRagdoll();
		GameObject.Destroy();
	}
}


public class RagdollSpawner : Component
{
	[Sync]
	public SkinnedModelRenderer Renderer { get; set; }

	private GameObject _ragdoll;

	protected override void OnDestroy()
	{
		_ragdoll?.Destroy();
	}

	protected override void OnEnabled()
	{
		if ( !Renderer.IsValid() )
			return;

		using var batch = Scene.BatchGroup();

		_ragdoll = new GameObject( true, "Ragdoll" );
		_ragdoll.Tags.Add( "ragdoll" );
		_ragdoll.WorldTransform = WorldTransform;

		var mainBody = _ragdoll.Components.Create<SkinnedModelRenderer>();
		mainBody.CopyFrom( Renderer );
		mainBody.UseAnimGraph = false;

		CopyClothing( mainBody );

		var physics = _ragdoll.Components.Create<ModelPhysics>();
		physics.Model = mainBody.Model;
		physics.Renderer = mainBody;

		// Must dispose batch before copying bones so physics bodies exist
		batch.Dispose();
		physics.CopyBonesFrom( Renderer, true );
	}

	private void CopyClothing( SkinnedModelRenderer mainBody )
	{
		var clothingRenderers = Renderer.GameObject.Children
			.SelectMany( x => x.Components.GetAll<SkinnedModelRenderer>() );

		foreach ( var clothing in clothingRenderers )
		{
			if ( !clothing.IsValid() )
				continue;

			var clothingObject = new GameObject( true, clothing.GameObject.Name );
			clothingObject.Parent = _ragdoll;

			var item = clothingObject.Components.Create<SkinnedModelRenderer>();
			item.CopyFrom( clothing );
			item.BoneMergeTarget = mainBody;
		}
	}
}
