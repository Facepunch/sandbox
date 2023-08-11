namespace Sandbox
{
	// If an entity implements this it'll receive an OnStopUsing when a player stops pushing the USE button
	public interface IStopUsing
	{
		void OnStopUsing( Entity user );
	}
}
