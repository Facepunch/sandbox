using Sandbox.Npcs.Schedules;

namespace Sandbox.Npcs.Scientist;

public class ScientistNpc : Npc, Component.IDamageable
{
	[Property, ClientEditable, Range( 1, 100 ), Sync]
	public float Health { get; set; } = 100f;

	[Property]
	public SkinnedModelRenderer Renderer { get; set; }

	private Vector3? _lastTarget;
	private TimeSince _timeSinceLostVision;

	public override ScheduleBase GetSchedule()
	{
		//
		// Update last known position if we can see a target
		//
		if ( Senses.VisibleTargets.Any() )
		{
			var visible = Senses.GetNearestVisible();
			if ( visible.IsValid() )
			{
				_lastTarget = visible.WorldPosition;
				_timeSinceLostVision = 0;
			}
		}

		//
		// Is someone in our face?
		//
		if ( Senses.DistanceToNearest <= Senses.PersonalSpace && Senses.Nearest.IsValid() )
		{
			var flee = GetSchedule<ScientistFleeSchedule>();
			flee.Source = Senses.Nearest;
			return flee;
		}

		if ( Senses.VisibleTargets.Any() )
		{
			var visible = Senses.GetNearestVisible();
			if ( visible.IsValid() )
			{
				var investigate = GetSchedule<ScientistInvestigateSchedule>();
				investigate.Target = visible;
				return investigate;
			}
		}

		if ( _lastTarget.HasValue && _timeSinceLostVision < 10f )
		{
			var search = GetSchedule<ScientistSearchSchedule>();
			search.Target = _lastTarget.Value;
			return search;
		}

		_lastTarget = null;
		return GetSchedule<ScientistIdleSchedule>();
	}

	void IDamageable.OnDamage( in DamageInfo damage )
	{
		if ( IsProxy )
			return;

		Health -= damage.Damage;

		if ( Health < 1 )
		{
			CreateRagdoll();
			GameObject.Destroy();
		}
	}

	/// <summary>
	/// Should this be a nice helper?
	/// </summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	void CreateRagdoll()
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
	}
}
