/// <summary>
/// Like the regular <see cref="Voice"/> component, but it can be muted.
/// </summary>
public class MuteableVoice : Voice
{
	/// <summary>
	/// A set of muted voices by their Steam Id.
	/// </summary>
	private static readonly HashSet<SteamId> Muted = new();

	/// <summary>
	/// Mute the voice of a Steam Id.
	/// </summary>
	public static void Mute( SteamId id )
	{
		Muted.Add( id );
	}
	
	/// <summary>
	/// Unmute the voice of a Steam Id.
	/// </summary>
	public static void Unmute( SteamId id )
	{
		Muted.Remove( id );
	}
	
	/// <summary>
	/// Whether a Steam Id is muted.
	/// </summary>
	public static bool IsMuted( SteamId id )
	{
		return Muted.Contains( id );
	}
	
	protected override bool ShouldHearVoice( Connection connection )
	{
		return !IsMuted( connection.SteamId );
	}
}
