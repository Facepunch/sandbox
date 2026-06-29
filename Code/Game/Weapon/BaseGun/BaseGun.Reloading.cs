using System.Threading;

public partial class BaseGun
{
	/// <summary>
	/// Should we consume 1 bullet per reload instead of filling the clip?
	/// </summary>
	[Property, Feature( "Ammo" )]
	public bool IncrementalReloading { get; set; } = false;

	/// <summary>
	/// Extra delay after the first shell reload before subsequent shells begin (e.g. longer carrier insertion animation).
	/// Only used with incremental reloading. If zero, no extra delay is added.
	/// </summary>
	[Property, Feature( "Ammo" ), ShowIf( nameof( IncrementalReloading ), true )]
	public float FirstShellReloadTime { get; set; } = 0f;

	/// <summary>
	/// Delay before the first shell is inserted during incremental reload.
	/// If zero, uses <see cref="ReloadTime"/>.
	/// </summary>
	[Property, Feature( "Ammo" ), ShowIf( nameof( IncrementalReloading ), true )]
	public float ReloadStartTime { get; set; } = 0f;

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

		Assert.True( Owner.Controller.IsValid(), "BaseGun::BroadcastReload - Player Controller is invalid!" );
		Assert.True( Owner.Controller.Renderer.IsValid(), "BaseGun::BroadcastReload - Renderer is invalid!" );

		Owner.Controller.Renderer.Set( "b_reload", true );
	}

	protected virtual async Task ReloadAsync( CancellationToken ct )
	{
		// Capture so we can tell if a newer reload has replaced us by the time finally runs.
		var mySource = reloadToken;
		var isFirstShell = ClipContents == 0;

		try
		{
			ViewModel?.RunEvent<ViewModel>( x => x.OnReloadStart() );

			BroadcastReload();

			var firstIteration = true;

			while ( ClipContents < ClipMaxSize && !ct.IsCancellationRequested )
				{
					var delay = (firstIteration && IncrementalReloading && ReloadStartTime > 0f) ? ReloadStartTime : ReloadTime;
					firstIteration = false;
					await Task.DelaySeconds( delay, ct );

					var needed = IncrementalReloading ? 1 : (ClipMaxSize - ClipContents);

					if ( WeaponConVars.InfiniteReserves )
					{
						ViewModel?.RunEvent<ViewModel>( x => x.OnIncrementalReload( isFirstShell ) );
						ClipContents += needed;
					}
					else
					{
						var available = Math.Min( needed, ReserveAmmo );

						if ( available <= 0 )
							break;

						ViewModel?.RunEvent<ViewModel>( x => x.OnIncrementalReload( isFirstShell ) );

						ReserveAmmo -= available;
						ClipContents += available;
					}

					// After the first shell, wait longer before the next one starts
					if ( isFirstShell && FirstShellReloadTime > 0f )
					{
						await Task.DelaySeconds( FirstShellReloadTime, ct );
					}

					isFirstShell = false;
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
