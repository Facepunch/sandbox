public sealed class WorldModel : WeaponModel
{
	// OnAttack (muzzle flash, brass, tracer) is handled by the engine BaseWeaponModel. The weapon
	// drives effects on whichever model is visible to each client, so no view-model dedup is needed.
}
