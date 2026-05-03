namespace Sandbox.Npcs;

public partial class Npc : Component
{
	/// <summary>
	/// The current running schedule for this NPC.
	/// </summary>
	public ScheduleBase ActiveSchedule { get; private set; }

	readonly Dictionary<Type, ScheduleBase> _schedules = [];

	/// <summary>
	/// Get a schedule -- if it doesn't exist, one will be created
	/// </summary>
	protected T GetSchedule<T>() where T : ScheduleBase, new()
	{
		var type = typeof(T);
		if ( !_schedules.TryGetValue( type, out var schedule ) )
		{
			schedule = new T();
			_schedules[type] = schedule;
		}

		return (T)schedule;
	}

	public virtual ScheduleBase GetSchedule()
	{
		return null;
	}

	/// <summary>
	/// Updates a behavior, returns if there is an active schedule - this will stop lower priority behaviors from running
	/// </summary>
	void TickSchedule()
	{
		if ( !NpcConVars.Enabled )
			return;

		// If we have a schedule, keep running it 
		// until it's completely finished.
		if ( ActiveSchedule is not null )
		{
			RunActiveSchedule();
			return;
		}

		var newSchedule = GetSchedule();
		if ( newSchedule is null ) return;

		ActiveSchedule = newSchedule;
		ActiveSchedule?.InternalInit( this );
	}

	private void RunActiveSchedule()
	{
		var status = ActiveSchedule.InternalUpdate();

		if ( status != TaskStatus.Running )
		{
			EndCurrentSchedule();
		}
	}


	protected override void OnDisabled()
	{
		EndCurrentSchedule();
	}

	/// <summary>
	/// End the current schedule cleanly. Can be called by subclasses to interrupt
	/// the active schedule (e.g. when damaged).
	/// </summary>
	protected void EndCurrentSchedule()
	{
		ActiveSchedule?.InternalEnd();
		ActiveSchedule = null;
	}
}
