/// <summary>
/// Manages loadout persistence and restoration for a player.
/// Lives on the Player GameObject alongside PlayerInventory.
/// Listens to inventory events to auto-save, and handles all loadout RPCs directly.
/// </summary>
public sealed class PlayerLoadout : Component, Local.IPlayerEvents, Global.IPlayerEvents, Global.ISaveEvents
{
	[RequireComponent] public Player Player { get; set; }
	[RequireComponent] public PlayerInventory Inventory { get; set; }

	private bool _isRestoringLoadout;

	/// <summary>
	/// One entry in a serialized loadout: the prefab resource path, plus the spawner payload for spawner weapons.
	/// </summary>
	public struct LoadoutEntry
	{
		public string PrefabPath { get; set; }
		public string SpawnerDataPayload { get; set; }
	}

	public string SerializeLoadout()
	{
		var entries = Inventory.Weapons
			.Where( w => !string.IsNullOrEmpty( w.GameObject.PrefabInstanceSource ) )
			.Select( w => new LoadoutEntry
			{
				PrefabPath = w.GameObject.PrefabInstanceSource,
				SpawnerDataPayload = (w as SpawnerWeapon)?.SpawnerData
			} )
			.ToList();

		return entries.Count > 0 ? Json.Serialize( entries ) : null;
	}

	public void SaveLoadout()
	{
		if ( _isRestoringLoadout ) return;

		var json = SerializeLoadout();
		if ( string.IsNullOrEmpty( json ) ) return;

		if ( Player.IsLocalPlayer )
		{
			LocalData.Set( "hotbar", json );
		}
		else
		{
			PushLoadoutToClient( json );
		}
	}

	public void GiveLoadoutWeapons( string json )
	{
		var entries = Json.Deserialize<List<LoadoutEntry>>( json );
		if ( entries is null ) return;

		_isRestoringLoadout = true;
		try
		{
			foreach ( var entry in entries )
			{
				if ( !Inventory.Pickup( entry.PrefabPath, false ) )
					continue;

				if ( !string.IsNullOrEmpty( entry.SpawnerDataPayload ) && Inventory.GetWeapon<SpawnerWeapon>() is { } spawnerWeapon )
				{
					spawnerWeapon.RestoreSpawnerData( entry.SpawnerDataPayload );
				}
			}
		}
		finally
		{
			_isRestoringLoadout = false;
		}
	}

	private static async Task EnsureMountedAsync( string json )
	{
		var entries = Json.Deserialize<List<LoadoutEntry>>( json );
		if ( entries is null ) return;

		var needsMounts = entries.Any( e => !string.IsNullOrEmpty( e.SpawnerDataPayload )
			&& e.SpawnerDataPayload.EndsWith( ".vmdl", StringComparison.OrdinalIgnoreCase ) );

		if ( !needsMounts ) return;

		foreach ( var entry in Sandbox.Mounting.Directory.GetAll().Where( e => e.Available ) )
			await Sandbox.Mounting.Directory.Mount( entry.Ident );
	}

	[Rpc.Owner]
	private void PushLoadoutToClient( string loadoutJson )
	{
		LocalData.Set( "hotbar", loadoutJson );
	}

	[Rpc.Owner]
	private void RequestClientLoadout()
	{
		var json = LocalData.Get<string>( "hotbar" );
		if ( !string.IsNullOrEmpty( json ) )
			HostRestoreLoadoutFromClient( json );
	}

	/// <summary>
	/// Clears the current inventory, waits a frame, then gives the loadout from JSON and equips the best weapon.
	/// </summary>
	private async Task ReplaceLoadoutAsync( string json )
	{
		foreach ( var weapon in Inventory.Weapons.ToList() )
			weapon.DestroyGameObject();

		await Task.Yield();

		await EnsureMountedAsync( json );
		GiveLoadoutWeapons( json );

		var best = Inventory.GetBestWeapon();
		if ( best.IsValid() )
			Inventory.SwitchWeapon( best );
	}

	[Rpc.Host]
	private async void HostRestoreLoadoutFromClient( string loadoutJson )
	{
		await ReplaceLoadoutAsync( loadoutJson );
	}

	void Global.IPlayerEvents.OnPlayerSpawned( Player player )
	{
		if ( player != Player ) return;
		if ( !Networking.IsHost ) return;

		_ = RestoreOnSpawnAsync();
	}

	private async Task RestoreOnSpawnAsync()
	{
		if ( Player.IsLocalPlayer )
		{
			var json = LocalData.Get<string>( "hotbar" );
			if ( !string.IsNullOrEmpty( json ) )
			{
				await ReplaceLoadoutAsync( json );
				return;
			}
		}
		else
		{
			RequestClientLoadout();
			return;
		}

		Inventory.GiveLoadout();
		var bestWeapon = Inventory.GetBestWeapon();
		if ( bestWeapon.IsValid() )
			Inventory.SwitchWeapon( bestWeapon );
	}

	void Local.IPlayerEvents.OnDied( PlayerDiedParams args )
	{
		if ( !Networking.IsHost ) return;
		SaveLoadout();
	}

	void Local.IPlayerEvents.OnPickup( PlayerPickupEvent e )
	{
		if ( e.Cancelled ) return;
		if ( !Networking.IsHost ) return;
		SaveLoadout();
	}

	void Local.IPlayerEvents.OnDrop( PlayerDropEvent e )
	{
		if ( e.Cancelled ) return;
		if ( !Networking.IsHost ) return;
		_ = SaveLoadoutAfterYield();
	}

	void Local.IPlayerEvents.OnRemoveWeapon( PlayerRemoveWeaponEvent e )
	{
		if ( e.Cancelled ) return;
		if ( !Networking.IsHost ) return;
		_ = SaveLoadoutAfterYield();
	}

	void Local.IPlayerEvents.OnMoveSlot( PlayerMoveSlotEvent e )
	{
		if ( e.Cancelled ) return;
		if ( !Networking.IsHost ) return;
		SaveLoadout();
	}

	private async Task SaveLoadoutAfterYield()
	{
		await Task.Yield();
		SaveLoadout();
	}

	void Global.ISaveEvents.BeforeSave( string filename )
	{
		if ( !Networking.IsHost ) return;

		var steamId = (long)(Player.Network.Owner?.SteamId ?? default);
		if ( steamId == 0 ) return;

		var json = SerializeLoadout();
		if ( string.IsNullOrEmpty( json ) ) return;

		SaveSystem.Current?.SetMetadata( $"Loadout_{steamId}", json );
	}

	void Global.ISaveEvents.AfterLoad( string filename )
	{
		if ( !Networking.IsHost ) return;

		var steamId = (long)(Player.Network.Owner?.SteamId ?? default);
		if ( steamId == 0 ) return;

		var json = SaveSystem.Current?.GetMetadata( $"Loadout_{steamId}" );
		if ( string.IsNullOrEmpty( json ) ) return;

		_ = RestoreLoadoutFromSaveAsync( json );
	}

	private async Task RestoreLoadoutFromSaveAsync( string json )
	{
		await ReplaceLoadoutAsync( json );
	}
}
