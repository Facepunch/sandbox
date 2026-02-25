

public partial class Toolgun : BaseCarryable
{
	[Header( "Effects" )]
	[Property] public GameObject SuccessImpactEffect { get; set; }
	[Property] public GameObject SuccessBeamEffect { get; set; }

	public void SpinCoil()
	{
		_coilSpin += 10;
	}


	void ApplyCoilSpin()
	{
		_coilSpin = _coilSpin.LerpTo( 0, Time.Delta * 1 );

		if ( !ViewModel.IsValid() ) return;

		var coil = ViewModel.GetAllObjects( true ).FirstOrDefault( x => x.Name == "coil" );
		coil.WorldRotation *= Rotation.From( 0, 0, _coilSpin );
	}

	bool ping = false;
	public void SwitchToolMode()
	{
		ping = !ping;
		WeaponModel?.Renderer?.Set( "firing_mode", ping ? 1 : 0 );
	}
}
