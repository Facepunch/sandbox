using Sandbox.Npcs.Layers;

namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Task that plays speech via the SpeechLayer. Waits for the speech to finish before completing.
/// Accepts either a SoundEvent or a plain string (which uses the fallback sound).
/// If LookAt is set, the NPC looks them in the eyes while talking.
/// </summary>
public class Say : TaskBase
{
	public SoundEvent Sound { get; set; }
	public string Message { get; set; }
	public float Duration { get; set; }
	public GameObject LookAt { get; set; }

	public Say( SoundEvent sound, float duration = 0f, GameObject lookAt = null )
	{
		Sound = sound;
		Duration = duration;
		LookAt = lookAt;
	}

	public Say( string message, float duration = 3f, GameObject lookAt = null )
	{
		Message = message;
		Duration = duration;
		LookAt = lookAt;
	}

	protected override void OnStart()
	{
		var speech = Npc.Speech;

		if ( Sound is not null )
		{
			speech.Say( Sound, Duration, LookAt );
		}
		else if ( !string.IsNullOrEmpty( Message ) )
		{
			speech.Say( Message, Duration, LookAt );
		}
	}

	protected override TaskStatus OnUpdate()
	{
		return Npc.Speech.IsSpeaking ? TaskStatus.Running : TaskStatus.Success;
	}
}
