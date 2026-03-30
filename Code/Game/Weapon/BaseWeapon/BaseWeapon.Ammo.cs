public partial class BaseWeapon
{
	/// <summary>
	/// Does this weapon consume ammo at all?
	/// </summary>
	[Property, FeatureEnabled( "Ammo" )] public bool UsesAmmo { get; set; } = true;

	/// <summary>
	/// Does this weapon use clips?
	/// </summary>
	[Property, Feature( "Ammo" )] public bool UsesClips { get; set; } = true;

	/// <summary>
	/// When reloading, we'll take ammo from the reserve as much as we can to fill to this amount.
	/// </summary>
	[Property, Feature( "Ammo" ), ShowIf( nameof( UsesClips ), true )] public int ClipMaxSize { get; set; } = 30;

	/// <summary>
	/// The default amount of bullets in a weapon's magazine on pickup.
	/// </summary>
	[Property, Feature( "Ammo" ), ShowIf( nameof( UsesClips ), true )] public int ClipContents { get; set; } = 20;

	/// <summary>
	/// The maximum reserve ammo this weapon can hold.
	/// </summary>
	[Property, Feature( "Ammo" )] public int MaxReserveAmmo { get; set; } = 120;

	/// <summary>
	/// The current reserve ammo on this weapon.
	/// </summary>
	[Sync, Property, Feature( "Ammo" )] public int ReserveAmmo { get; set; } = 0;

	/// <summary>
	/// How much reserve ammo this weapon starts with on pickup.
	/// </summary>
	[Property, Feature( "Ammo" )] public int StartingAmmo { get; set; } = 0;

	/// <summary>
	/// How long does it take to reload?
	/// </summary>
	[Property, Feature( "Ammo" )] public float ReloadTime { get; set; } = 2.5f;
	
	/// <summary>
	/// Can we switch to this gun?
	/// </summary>
	public override bool CanSwitch()
	{
		return true;
	}

	/// <summary>
	/// Takes ammo from the clip, or from reserve if not using clips.
	/// </summary>
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

		// No clips — take directly from reserve
		if ( ReserveAmmo < count )
			return false;

		ReserveAmmo -= count;
		return true;
	}

	/// <summary>
	/// Do we have ammo?
	/// </summary>
	public bool HasAmmo()
	{
		if ( !UsesAmmo ) return true;

		if ( UsesClips )
			return ClipContents > 0;

		return ReserveAmmo > 0;
	}

	/// <summary>
	/// Adds reserve ammo to this weapon, clamped to max.
	/// </summary>
	public int AddReserveAmmo( int count )
	{
		var space = MaxReserveAmmo - ReserveAmmo;
		var toAdd = Math.Min( count, space );
		ReserveAmmo += toAdd;
		return toAdd;
	}
}
