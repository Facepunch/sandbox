public sealed class WorldModel : WeaponModel
{
	public override void OnAttack()
	{
		Renderer?.Set( "b_attack", true );

		DoMuzzleEffect();
		DoEjectBrass();
	}

	public override void CreateRangedEffects( BaseWeapon weapon, Vector3 hitPoint, Vector3? origin )
	{
		if ( weapon.ViewModel.IsValid() )
			return;

		DoTracerEffect( hitPoint, origin );
	}
}
