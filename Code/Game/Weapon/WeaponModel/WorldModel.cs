public sealed class WorldModel : WeaponModel
{
	public void OnAttack()
	{
		Renderer?.Set( "b_attack", true );

		DoMuzzleEffect();
		DoEjectBrass();
	}

	public void CreateRangedEffects( BaseWeapon weapon, Vector3 hitPoint, Vector3? origin )
	{
		if ( weapon.ViewModel.IsValid() )
			return;

		DoTracerEffect( hitPoint, origin );
	}
}
