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
	/// Whether the NPC is currently speaking. Voice lines usually have no subtitle,
	/// so this has to watch the sound itself, not just the subtitle text.
	/// </summary>
	public bool IsSpeaking => CurrentSpeech is not null || (_soundHandle.IsValid() && !_soundHandle.IsStopped);

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
	private GameObject _speechTarget;

	/// <summary>
	/// Whether the cooldown has elapsed and the NPC can speak again.
	/// </summary>
	public bool CanSpeak => _lastSpoke > Cooldown;

	/// <summary>
	/// Play a sound event and show its subtitle (if one exists) above the NPC.
	/// If <paramref name="lookAt"/> is given, the NPC looks them in the eyes
	/// while talking.
	/// </summary>
	public void Say( SoundEvent sound, float duration = 0f, GameObject lookAt = null )
	{
		Say( sound, null, duration, lookAt );
	}

	/// <summary>
	/// Play a sound event with an explicit subtitle override.
	/// </summary>
	public void Say( SoundEvent sound, string subtitle, float duration = 0f, GameObject lookAt = null )
	{
		if ( sound is null ) return;

		// Stop any existing speech
		Stop();

		_speechTarget = lookAt;

		// Resolve the sound file host-side so every client plays the same one.
		var soundFile = Game.Random.FromList( sound.Sounds );
		if ( !soundFile.IsValid() ) return;

		PlaySound( soundFile, sound.Volume.GetValue(), sound.Pitch.GetValue() );

		if ( !string.IsNullOrEmpty( subtitle ) )
		{
			CurrentSpeech = subtitle;
		}

		_subtitleEnd = duration;
		_lastSpoke = 0;
	}

	// AI runs host-side, so broadcast the sound to every client -- otherwise only the host hears it.
	[Rpc.Broadcast]
	private void PlaySound( SoundFile soundFile, float volume, float pitch )
	{
		if ( !soundFile.IsValid() ) return;

		// Speak through the renderer so the NPC lipsyncs to the sound
		if ( Npc.IsValid() && Npc.Renderer.IsValid() )
		{
			_soundHandle = Npc.Renderer.SpeakSound( soundFile, volume, pitch, Npc.GameObject );
			return;
		}

		_soundHandle = Sound.PlayFile( soundFile, volume, pitch );

		if ( _soundHandle.IsValid() )
		{
			_soundHandle.Parent = Npc.GameObject;
		}
	}

	/// <summary>
	/// Say a string message using the fallback sound, with the string shown as a subtitle.
	/// </summary>
	public void Say( string message, float duration = 3f, GameObject lookAt = null )
	{
		if ( string.IsNullOrEmpty( message ) ) return;

		if ( FallbackSound is not null )
		{
			Say( FallbackSound, message, duration, lookAt );
		}
		else
		{
			// No fallback sound — just show the subtitle for the duration
			Stop();
			_speechTarget = lookAt;
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
		_speechTarget = null;
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
		// The Npc can be gone while we're being torn down (death destroys the hierarchy).
		if ( !Npc.IsValid() )
			return;

		// Only the host manages speech state (sound playback, duration tracking)
		if ( !IsProxy && IsFinished && (CurrentSpeech is not null || _speechTarget is not null) )
		{
			CurrentSpeech = null;
			_speechTarget = null;
		}

		// Look whoever we're talking to in the eyes while we speak. Re-armed each
		// frame so the gaze lingers for a moment after the line ends, then hands
		// back to whatever the NPC was looking at before.
		if ( !IsProxy && IsSpeaking && _speechTarget.IsValid() )
		{
			Npc.Animation?.AddLookTarget( _speechTarget, 1f );
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
		var camera = Npc.Scene.Camera;
		if ( !camera.IsValid() ) return;

		var worldPos = Npc.WorldPosition + Vector3.Up * 80f;
		var screenPos = camera.PointToScreenPixels( worldPos, out var behind );
		if ( behind ) return;

		// Don't show subtitles through walls
		var tr = Npc.Scene.Trace.Ray( camera.WorldPosition, worldPos )
			.WithTag( "world" )
			.Run();

		if ( tr.Hit ) return;

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
