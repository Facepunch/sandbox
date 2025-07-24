public sealed partial class Player
{
	[Sync]
	public NetDictionary<AmmoResource, int> AmmoCounts { get; set; } = new();

	public int GetAmmoCount( AmmoResource resource )
	{
		if ( resource == null ) return default;
		return AmmoCounts.GetValueOrDefault( resource, 0 );
	}

	[Rpc.Owner]
	public void GiveAmmo( AmmoResource resource, int count, bool notice )
	{
		if ( resource == null )
			return;

		if ( GetAmmoCount( resource ) + count > resource.MaxAmount )
		{
			count = resource.MaxAmount - GetAmmoCount( resource );
		}

		if ( count <= 0 )
			return;

		var amountGained = AddAmmoCount( resource, count );

		if ( notice && amountGained > 0 )
			ShowNotice( $"{resource.AmmoType} x {amountGained}" );
	}

	public int SetAmmoCount( AmmoResource resource, int count )
	{
		return AmmoCounts[resource] = count;
	}

	public int AddAmmoCount( AmmoResource resource, int count )
	{
		var amountToGain = Math.Min( count, resource.MaxAmount - GetAmmoCount( resource ) );

		AmmoCounts[resource] = GetAmmoCount( resource ) + amountToGain;

		return amountToGain;
	}

	public int SubtractAmmoCount( AmmoResource resource, int count )
	{
		var current = GetAmmoCount( resource );

		count = Math.Min( count, current );
		if ( count <= 0 )
			return 0;

		var to = current - count;

		AmmoCounts[resource] = to;
		return count;
	}
}
