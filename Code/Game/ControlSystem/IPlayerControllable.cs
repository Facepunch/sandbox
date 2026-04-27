public interface IPlayerControllable
{
	/// <summary>
	/// Whether this controllable can currently be controlled by a seated player.
	/// </summary>
	public bool CanControl( Player player ) => true;
	public void OnStartControl() { }
	public void OnEndControl() { }
	public void OnControl();
}
