/// <summary>
/// The burst of particles on something being frozen or unfrozen, by the physgun or while driving it.
/// </summary>
public static class FreezeEffect
{
	const string FreezePrefab = "/weapons/physgun/effects/freeze.prefab";
	const string UnfreezePrefab = "/weapons/physgun/effects/unfreeze.prefab";

	/// <summary>
	/// Play the freeze or unfreeze effect on <paramref name="target"/>, here only. Callers broadcast it themselves.
	/// </summary>
	public static void Play( GameObject target, bool frozen )
	{
		if ( !target.IsValid() ) return;
		if ( ResourceLibrary.Get<PrefabFile>( frozen ? FreezePrefab : UnfreezePrefab ) is not { } prefab ) return;

		var effect = GameObject.Clone( prefab, new CloneConfig { Transform = target.WorldTransform, StartEnabled = true } );
		foreach ( var emitter in effect.GetComponentsInChildren<ParticleModelEmitter>() )
		{
			emitter.Target = target;
		}
	}
}
