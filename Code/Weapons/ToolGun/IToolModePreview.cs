/// <summary>
/// Implement on a Component to receive a per-frame callback while a player
/// nearby is holding any tool. Draw immediate debug overlays here.
/// </summary>
public interface IToolModePreview
{
	void OnToolModePreview();
}
