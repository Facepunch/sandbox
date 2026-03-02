namespace Sandbox.Npcs;

/// <summary>
/// Extends a SoundFile to add subtitle text that will be shown when the sound is played. This is used for Npc speech.
/// </summary>
[AssetType( Name = "Subtitle", Category = "Sounds", Extension = "ssub" )]
public partial class SubtitleExtension : ResourceExtension<SoundFile, SubtitleExtension>
{
	/// <summary>
	/// The text to show in the subtitle for this sound event. If empty, no subtitle will be shown.
	/// </summary>
	[Property, TextArea] public string Text { get; set; }
}
