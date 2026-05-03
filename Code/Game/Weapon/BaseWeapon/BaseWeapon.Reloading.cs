using System.Threading;

public partial class BaseWeapon
{
	/// <summary>
	/// Should we consume 1 bullet per reload instead of filling the clip?
	/// </summary>
	[Property, Feature( "Ammo" )]
	public bool IncrementalReloading { get; set; } = false;

	/// <summary>
	/// Can we cancel reloads?
	/// </summary>
	[Property, Feature( "Ammo" )]
	public bool CanCancelReload { get; set; } = true;

	private CancellationTokenSource reloadToken;
	private bool isReloading;

	public bool CanReload()
	{
		if ( !UsesClips ) return false;
		if ( ClipContents >= ClipMaxSize ) return false;
		if ( isReloading ) return false;
		if ( !WeaponConVars.InfiniteReserves && ReserveAmmo <= 0 ) return false;

		return true;
	}

	public bool IsReloading() => isReloading;

	public virtual void CancelReload()
	{
		if ( reloadToken?.IsCancellationRequested == false )
		{
			reloadToken?.Cancel();
			isReloading = false;

			ViewModel?.RunEvent<ViewModel>( x => x.OnReloadCancel() );
		}
	}

	public virtual async void OnReloadStart()
	{
		if ( !CanReload() )
			return;

		CancelReload();

		var cts = new CancellationTokenSource();
		reloadToken = cts;
		isReloading = true;

		try
		{
			await ReloadAsync( cts.Token );
		}
		finally
		{
			// Only clean up our own reload
			if ( reloadToken == cts )
			{
				isReloading = false;
				reloadToken = null;
			}
			cts.Dispose();
		}
	}

	[Rpc.Broadcast]
	private void BroadcastReload()
	{
		if ( !HasOwner ) return;

		Assert.True( Owner.Controller.IsValid(), "BaseWeapon::BroadcastReload - Player Controller is invalid!" );
		Assert.True( Owner.Controller.Renderer.IsValid(), "BaseWeapon::BroadcastReload - Renderer is invalid!" );

		Owner.Controller.Renderer.Set( "b_reload", true );
	}

	protected virtual async Task ReloadAsync( CancellationToken ct )
	{
		// Capture so we can tell if a newer reload has replaced us by the time finally runs.
		var mySource = reloadToken;

		try
		{
			ViewModel?.RunEvent<ViewModel>( x => x.OnReloadStart() );

			BroadcastReload();

			while ( ClipContents < ClipMaxSize && !ct.IsCancellationRequested )
			{
				await Task.DelaySeconds( ReloadTime, ct );

				var needed = IncrementalReloading ? 1 : (ClipMaxSize - ClipContents);

				if ( WeaponConVars.InfiniteReserves )
				{
					ViewModel?.RunEvent<ViewModel>( x => x.OnIncrementalReload() );
					ClipContents += needed;
				}
				else
				{
					var available = Math.Min( needed, ReserveAmmo );

					if ( available <= 0 )
						break;

					ViewModel?.RunEvent<ViewModel>( x => x.OnIncrementalReload() );

					ReserveAmmo -= available;
					ClipContents += available;
				}
			}
		}
		finally
		{
			if ( reloadToken == mySource )
			{
				ViewModel?.RunEvent<ViewModel>( x => x.OnReloadFinish() );
			}
		}
	}
}
