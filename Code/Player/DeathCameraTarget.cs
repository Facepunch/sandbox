public partial class DeathCameraTarget : Component
{
	public Connection Connection { get; set; }
	public DateTime Created { get; set; }

	protected override void OnEnabled()
	{
		Invoke( 60.0f, GameObject.Destroy );
	}
}
