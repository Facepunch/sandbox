using Sandbox.Npcs.Schedules;

namespace Sandbox.Npcs.Scientist;

public class ScientistNpc : Npc
{
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
}
