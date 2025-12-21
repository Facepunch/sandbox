namespace Sandbox.Npcs;

/// <summary>
/// A schedule -- can be understood as a way to execute a sequence of tasks
/// </summary>
public abstract class ScheduleBase
{
	public Npc Npc { get; private set; }
	protected GameObject GameObject => Npc.GameObject;

	private List<TaskBase> _tasks = new();
	private int _currentTaskIndex = 0;

	/// <summary>
	/// Initialize the schedule with the Behavior context
	/// </summary>
	internal void InternalInit( Npc npc )
	{
		Npc = npc;
		_tasks.Clear();
		_currentTaskIndex = 0;

		// Build task sequence
		OnStart();

		// Start first task
		StartCurrentTask();
	}

	protected virtual void OnUpdate()
	{
		//
	}

	protected virtual bool ShouldCancel()
	{
		return false;
	}

	/// <summary>
	/// Called when task is ended because of ShouldCancel returning true
	/// </summary>
	protected virtual void OnCancelled()
	{

	}

	/// <summary>
	/// Called every frame while schedule is running
	/// </summary>
	internal TaskStatus InternalUpdate()
	{
		if ( _tasks.Count == 0 ) return TaskStatus.Failed;
		if ( _currentTaskIndex >= _tasks.Count ) return TaskStatus.Success;

		// Give schedule a chance to cancel itself, based on interuptions
		if ( ShouldCancel() )
		{
			OnCancelled();
			return TaskStatus.Interrupted;
		}

		var currentTask = _tasks[_currentTaskIndex];
		var status = currentTask.InternalUpdate();

		if ( status is not TaskStatus.Running )
		{
			currentTask.InternalEnd();

			if ( status is TaskStatus.Success )
			{
				_currentTaskIndex++;
				StartCurrentTask();
				return TaskStatus.Running;
			}

			return status;
		}

		return TaskStatus.Running;
	}

	/// <summary>
	/// Called once when schedule ends
	/// </summary>
	internal void InternalEnd()
	{
		// End current task if running
		if ( _currentTaskIndex < _tasks.Count )
		{
			_tasks[_currentTaskIndex].InternalEnd();
		}

		_currentTaskIndex = 0;

		OnEnd();
	}

	/// <summary>
	/// Called when this schedule starts -- this is where you can add tasks to run
	/// </summary>
	protected virtual void OnStart() { }

	/// <summary>
	/// Called when this schedule ends -- this is where you can clean stuff up
	/// </summary>
	protected virtual void OnEnd() { }

	/// <summary>
	/// Add a task to the sequence
	/// </summary>
	protected void AddTask( TaskBase task )
	{
		_tasks.Add( task );
	}

	/// <summary>
	/// Start the current task in sequence
	/// </summary>
	private void StartCurrentTask()
	{
		if ( _currentTaskIndex < _tasks.Count )
		{
			var task = _tasks[_currentTaskIndex];
			task.Initialize( this );
		}
	}

	/// <summary>
	/// Information about this schedule for debugging purposes
	/// </summary>
	public string GetDebugString()
	{
		if ( _currentTaskIndex >= _tasks.Count )
			return $"{GetType().Name}/(none)";

		var task = _tasks[_currentTaskIndex];

		return $"{GetType().Name}/{task.GetType().Name}";
	}
}
