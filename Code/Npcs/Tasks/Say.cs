using Sandbox.Npcs.Layers;

namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Task that plays speech via the SpeechLayer. Waits for the speech to finish before completing.
/// Accepts either a SoundEvent or a plain string (which uses the fallback sound).
/// </summary>
public class Say : TaskBase
{
	public SoundEvent Sound { get; set; }
	public string Message { get; set; }
	public float Duration { get; set; }

	public Say( SoundEvent sound, float duration = 0f )
	{
		Sound = sound;
		Duration = duration;
	}

	public Say( string message, float duration = 3f )
	{
		Message = message;
		Duration = duration;
	}

	protected override void OnStart()
	{
		var speech = Npc.Speech;

		if ( Sound is not null )
		{
			speech.Say( Sound, Duration );
		}
		else if ( !string.IsNullOrEmpty( Message ) )
		{
			speech.Say( Message, Duration );
		}
	}

	protected override TaskStatus OnUpdate()
	{
		return Npc.Speech.IsSpeaking ? TaskStatus.Running : TaskStatus.Success;
	}
}
