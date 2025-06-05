public partial class BaseWeapon
{
	/// <summary>
	/// Does this weapon consume ammo at all?
	/// </summary>
	[Property, FeatureEnabled( "Ammo" )] public bool UsesAmmo { get; set; } = true;

	/// <summary>
	/// Is this weapon ammo for itself? eg tripmine, grenades
	/// </summary>
	[Property, Feature( "Ammo" )] public bool IsSelfAmmo { get; set; } = false;

	/// <summary>
	/// The <see cref="AmmoResource"/> for this weapon
	/// </summary>
	[Property, Feature( "Ammo" )] public AmmoResource AmmoResource { get; set; }
	
	/// <summary>
	/// Does this weapon use clips?
	/// </summary>
	[Property, Feature( "Ammo" )] public bool UsesClips { get; set; } = true;

	/// <summary>
	/// When reloading, we'll take ammo from the player as much as we can to fill to this amount.
	/// </summary>
	[Property, Feature( "Ammo" ), ShowIf( nameof( UsesClips ), true )] public int ClipMaxSize { get; set; } = 30;

	/// <summary>
	/// The default amount of bullets in a weapon's magazine on pickup. This can differ to the max size.
	/// </summary>
	[Property, Feature( "Ammo" ), ShowIf( nameof( UsesClips ), true )] public int ClipContents { get; set; } = 20;

	/// <summary>
	/// StartingAmmo defines how much ammo we'll give to the player on pickup.
	/// </summary>
	[Property, Feature( "Ammo" )] public int StartingAmmo { get; set; } = 0;

	/// <summary>
	/// How long does it take to reload?
	/// </summary>
	[Property, Feature( "Ammo" )] public float ReloadTime { get; set; } = 2.5f;
	
	/// <summary>
	/// Can we switch to this gun?
	/// </summary>
	/// <returns></returns>
	public override bool CanSwitch()
	{
		return HasAmmo() || CanReload();
	}

	/// <summary>
	/// Takes ammo from the player's inventory
	/// </summary>
	/// <param name="count"></param>
	/// <returns></returns>
	public bool TakeAmmo( int count )
	{
		if ( !UsesAmmo ) return true;

		if ( UsesClips )
		{
			if ( ClipContents < count )
				return false;

			ClipContents -= count;
			return true;
		}

		return TakeAmmo( count, AmmoResource );
	}

	/// <summary>
	/// Takes ammo from the player's inventory
	/// </summary>
	/// <param name="count"></param>
	/// <param name="ammoType"></param>
	/// <returns></returns>
	public bool TakeAmmo( int count, AmmoResource ammoType )
	{
		if ( !UsesAmmo ) return true;

		var owner = Owner;

		if ( owner is null )
			return false;

		if ( owner.GetAmmoCount( ammoType ) < count )
			return false;

		owner.SubtractAmmoCount( ammoType, count );
		return true;
	}

	/// <summary>
	/// Do we have ammo for the weapon's ammo type?
	/// </summary>
	/// <returns></returns>
	public bool HasAmmo()
	{
		if ( !UsesAmmo ) return true;

		if ( UsesClips )
			return ClipContents > 0;

		return HasAmmo( AmmoResource );
	}

	/// <summary>
	/// Do we have ammo for a specific ammo type? Useful if a weapon has an alt fire.
	/// </summary>
	/// <param name="ammoType"></param>
	/// <returns></returns>
	public bool HasAmmo( AmmoResource ammoType )
	{
		if ( !UsesAmmo ) return true;

		var owner = Owner;

		if ( owner is null )
			return false;

		return owner.GetAmmoCount( ammoType ) > 0;
	}
}
