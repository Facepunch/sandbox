namespace Sandbox.Npcs.Layers;

/// <summary>
/// Manages NPC speech state, plays sound files, and renders subtitle text above their head.
/// </summary>
public class SpeechLayer : BaseNpcLayer
{
	/// <summary>
	/// The subtitle text currently being shown, if any. Synced to all clients.
	/// </summary>
	[Sync] public string CurrentSpeech { get; set; }

	/// <summary>
	/// Whether the NPC is currently speaking.
	/// </summary>
	public bool IsSpeaking => CurrentSpeech is not null;

	/// <summary>
	/// Minimum seconds between speeches.
	/// </summary>
	public float Cooldown { get; set; } = 8f;

	/// <summary>
	/// A generic fallback sound (e.g. a grunt or mumble) played when we're talking without a specific sound.
	/// </summary>
	public SoundEvent FallbackSound { get; set; }

	private SoundHandle _soundHandle;
	private TimeSince _lastSpoke;
	private TimeUntil _subtitleEnd;

	/// <summary>
	/// Whether the cooldown has elapsed and the NPC can speak again.
	/// </summary>
	public bool CanSpeak => _lastSpoke > Cooldown;

	/// <summary>
	/// Play a sound event and show its subtitle (if one exists) above the NPC.
	/// </summary>
	public void Say( SoundEvent sound, float duration = 0f )
	{
		Say( sound, null, duration );
	}

	/// <summary>
	/// Play a sound event with an explicit subtitle override.
	/// </summary>
	public void Say( SoundEvent sound, string subtitle, float duration = 0f )
	{
		if ( sound is null ) return;

		// Stop any existing speech
		Stop();

		// Resolve the next sound file from the event
		var soundFile = Game.Random.FromList( sound.Sounds );
;		if ( !soundFile.IsValid() ) return;

		// Play using the event's volume and pitch
		_soundHandle = Sound.PlayFile( soundFile, sound.Volume.GetValue(), sound.Pitch.GetValue() );

		if ( _soundHandle.IsValid() )
		{
			_soundHandle.Parent = Npc.GameObject;
		}

		// Use the explicit subtitle, or fall back to the extension on the resolved sound file
		if ( !string.IsNullOrEmpty( subtitle ) )
		{
			CurrentSpeech = subtitle;
		}
		else
		{
			var ext = SubtitleExtension.FindForResourceOrDefault( soundFile );
			CurrentSpeech = ext?.Text;
		}

		_subtitleEnd = duration;
		_lastSpoke = 0;
	}

	/// <summary>
	/// Say a string message using the fallback sound, with the string shown as a subtitle.
	/// </summary>
	public void Say( string message, float duration = 3f )
	{
		if ( string.IsNullOrEmpty( message ) ) return;

		if ( FallbackSound is not null )
		{
			Say( FallbackSound, message, duration );
		}
		else
		{
			// No fallback sound — just show the subtitle for the duration
			Stop();
			CurrentSpeech = message;
			_subtitleEnd = duration;
			_lastSpoke = 0;
		}
	}

	/// <summary>
	/// Stop any current speech and sound.
	/// </summary>
	public void Stop()
	{
		if ( _soundHandle.IsValid() )
		{
			_soundHandle.Stop();
		}

		CurrentSpeech = null;
	}

	/// <summary>
	/// Whether the sound has finished and the subtitle duration has elapsed.
	/// </summary>
	private bool IsFinished
	{
		get
		{
			var soundDone = !_soundHandle.IsValid() || _soundHandle.IsStopped;
			return soundDone && _subtitleEnd;
		}
	}

	protected override void OnUpdate()
	{
		// Only the host manages speech state (sound playback, duration tracking)
		if ( !IsProxy && CurrentSpeech is not null && IsFinished )
		{
			CurrentSpeech = null;
		}

		// All clients draw the subtitle when speech is active
		if ( CurrentSpeech is not null )
		{
			DrawSpeech();
		}
	}

	/// <summary>
	/// Draw a simple speech bubble above the NPC.
	/// </summary>
	private void DrawSpeech()
	{
		var worldPos = Npc.WorldPosition + Vector3.Up * 80f;
		var screenPos = Npc.Scene.Camera.PointToScreenPixels( worldPos, out var behind );
		if ( behind ) return;

		var text = TextRendering.Scope.Default;
		text.Text = CurrentSpeech;
		text.FontSize = 14;
		text.FontName = "Poppins";
		text.FontWeight = 500;
		text.TextColor = Color.White;
		text.Outline = new TextRendering.Outline { Color = Color.Black.WithAlpha( 0.8f ), Size = 3, Enabled = true };
		text.FilterMode = Rendering.FilterMode.Point;

		Npc.DebugOverlay.ScreenText( screenPos, text, TextFlag.CenterBottom );
	}

	public override void ResetLayer()
	{
		Stop();
	}
}
