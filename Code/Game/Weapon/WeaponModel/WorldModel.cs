using static BaseWeapon;

public sealed class WorldModel : WeaponModel, IWeaponEvent
{
	void IWeaponEvent.OnAttack( IWeaponEvent.AttackEvent e )
	{
		Renderer?.Set( "b_attack", true );

		if ( e.isFirstPerson )
			return;

		DoMuzzleEffect();
		DoEjectBrass();
	}

	void IWeaponEvent.CreateRangedEffects( BaseWeapon weapon, Vector3 hitPoint, Vector3? origin )
	{
		if ( weapon.ViewModel.IsValid() )
			return;

		DoTracerEffect( hitPoint, origin );
	}
}
