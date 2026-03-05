/// <summary>
/// Add to the sniper viewmodel prefab. Looks up the parent SniperWeapon
/// and pushes the viewmodel down out of view when scoped.
/// </summary>
public sealed class SniperViewModel : Component, ICameraSetup
{
	private float _offset;

	void ICameraSetup.PostSetup( CameraComponent cc )
	{
		var weapon = GetComponentInParent<SniperWeapon>();
		if ( !weapon.IsValid() ) return;

		var target = weapon.IsScoped ? 3 : 0f;
		_offset = _offset.LerpTo( target, Time.Delta * 15f );

		if ( _offset > 0.01f )
		{
			WorldPosition += cc.WorldRotation.Down * _offset;
		}
	}
}
