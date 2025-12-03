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

		var owner = Owner;
		if ( !owner.IsValid() || owner.GetAmmoCount( AmmoResource ) <= 0 )
			return false;

		return true;
	}

	public bool IsReloading() => isReloading;

	public virtual void CancelReload()
	{
		if ( reloadToken?.IsCancellationRequested == false )
		{
			reloadToken?.Cancel();
			isReloading = false;
		}
	}

	public virtual async void OnReloadStart()
	{
		if ( !CanReload() )
			return;

		CancelReload();

		try
		{
			reloadToken = new CancellationTokenSource();
			isReloading = true;

			await ReloadAsync( reloadToken.Token );
		}
		finally
		{
			reloadToken?.Dispose();
			reloadToken = null;
		}
	}

	[Rpc.Broadcast]
	private void BroadcastReload()
	{
		if ( !Owner.IsValid() ) return;

		Assert.True( Owner.Controller.IsValid(), "BaseWeapon::BroadcastReload - Player Controller is invalid!" );
		Assert.True( Owner.Controller.Renderer.IsValid(), "BaseWeapon::BroadcastReload - Renderer is invalid!" );

		Owner.Controller.Renderer.Set( "b_reload", true );
	}

	public virtual async Task ReloadAsync( CancellationToken ct )
	{
		try
		{
			ViewModel?.RunEvent<ViewModel>( x => x.OnReloadStart() );

			BroadcastReload();

			while ( ClipContents < ClipMaxSize && !ct.IsCancellationRequested )
			{
				await Task.DelaySeconds( ReloadTime, ct );

				var owner = Owner;
				if ( owner.IsValid() )
				{
					var needed = IncrementalReloading ? 1 : (ClipMaxSize - ClipContents);
					var available = owner.SubtractAmmoCount( AmmoResource, needed );

					if ( available <= 0 )
						break;

					ClipContents += available;
				}
				else
				{
					ClipContents = ClipMaxSize;
				}

				ViewModel?.RunEvent<ViewModel>( x => x.OnIncrementalReload() );

			}

			if ( ClipContents > 0 )
			{
				ViewModel?.RunEvent<ViewModel>( x => x.OnReloadFinish() );
			}
		}
		finally
		{
			reloadToken?.Cancel();
			isReloading = false;
		}
	}
}
