public partial class Toolgun : BaseCarryable
{
	public override void OnCameraMove( Player player, ref Angles angles )
	{
		base.OnCameraMove( player, ref angles );
	}

	public override void OnControl( Player player )
	{
		UpdateViewmodelScreen();

		base.OnControl( player );
	}
}
