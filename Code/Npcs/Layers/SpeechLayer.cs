namespace Sandbox.Npcs.Layers;

/// <summary>
/// Manages NPC speech state and renders speech text above their head.
/// </summary>
public class SpeechLayer : BaseNpcLayer
{
	/// <summary>
	/// The message currently being spoken, if any.
	/// </summary>
	public string CurrentSpeech { get; private set; }

	/// <summary>
	/// Whether the NPC is currently speaking.
	/// </summary>
	public bool IsSpeaking => !string.IsNullOrEmpty( CurrentSpeech );

	/// <summary>
	/// Minimum seconds between speeches.
	/// </summary>
	public float Cooldown { get; set; } = 8f;

	private TimeUntil _speechEnd;
	private TimeSince _lastSpoke;

	/// <summary>
	/// Whether the cooldown has elapsed and the NPC can speak again.
	/// </summary>
	public bool CanSpeak => _lastSpoke > Cooldown;

	/// <summary>
	/// Say something for a given duration.
	/// </summary>
	public void Say( string message, float duration = 3f )
	{
		if ( string.IsNullOrEmpty( message ) ) return;

		CurrentSpeech = message;
		_speechEnd = duration;
		_lastSpoke = 0;
	}

	protected override void OnUpdate()
	{
		if ( IsSpeaking && _speechEnd )
		{
			CurrentSpeech = null;
		}

		if ( IsSpeaking )
		{
			DrawSpeech();
		}
	}

	/// <summary>
	/// Draw a simple speech bubble above the NPC.
	/// </summary>
	private void DrawSpeech()
	{
		var bounds = Npc.GameObject.GetBounds();
		var worldPos = Npc.WorldPosition + Vector3.Up * (bounds.Size.z + 1f);
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

	public override void Reset()
	{
		CurrentSpeech = null;
	}
}
