public partial class Toolgun : ScreenWeapon
{
	[Header( "Effects" )]
	[Property] public GameObject SuccessImpactEffect { get; set; }
	[Property] public GameObject SuccessBeamEffect { get; set; }

	bool ping = false;
	public void SwitchToolMode()
	{
		ping = !ping;
		WeaponModel?.Renderer?.Set( "firing_mode", ping ? 1 : 0 );
	}
}
