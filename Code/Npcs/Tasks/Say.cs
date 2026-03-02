using Sandbox.Npcs.Layers;

namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Task that triggers speech via the SpeechLayer. Waits for the speech duration before completing.
/// </summary>
public class Say : TaskBase
{
	public string Message { get; set; }
	public float Duration { get; set; }

	private TimeUntil _endTime;

	public Say( string message, float duration = 3f )
	{
		Message = message;
		Duration = duration;
	}

	protected override void OnStart()
	{
		var speech = Npc.Layers.OfType<SpeechLayer>().FirstOrDefault();
		speech?.Say( Message, Duration );

		_endTime = Duration;
	}

	protected override TaskStatus OnUpdate()
	{
		return _endTime ? TaskStatus.Success : TaskStatus.Running;
	}
}
