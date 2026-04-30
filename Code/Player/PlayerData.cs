/// <summary>
/// Holds persistent player information like deaths, kills
/// </summary>
public sealed partial class PlayerData : Component, Global.ISaveEvents
{
	/// <summary>
	/// Unique Id per each player and bot, equal to owning Player connection Id if it's a real player.
	/// </summary>
	[Property] public Guid PlayerId { get; set; }
	[Property] public long SteamId { get; set; } = -1L;
	[Property] public string DisplayName { get; set; }

	[Sync] public int Kills { get; set; }
	[Sync] public int Deaths { get; set; }

	[Sync] public bool IsGodMode { get; set; }

	public Connection Connection => Connection.Find( PlayerId );

	/// <summary>
	/// Is this player data me?
	/// </summary>
	public bool IsMe => PlayerId == Connection.Local.Id;

	/// <inheritdoc cref="Connection.Ping"/>
	public float Ping => Connection?.Ping ?? 0;

	/// <summary>
	/// Data for all players
	/// </summary>
	public static IEnumerable<PlayerData> All => Game.ActiveScene.GetAll<PlayerData>();

	/// <summary>
	/// Get player data for a player
	/// </summary>
	/// <param name="connection"></param>
	/// <returns></returns>
	public static PlayerData For( Connection connection ) => connection == null ? default : For( connection.Id );

	/// <summary>
	/// Get player data for a player's id
	/// </summary>
	/// <param name="playerId"></param>
	/// <returns></returns>
	public static PlayerData For( Guid playerId )
	{
		return All.FirstOrDefault( x => x.PlayerId == playerId );
	}

	// Host-side respawn tracking. No sync required.
	private bool _needsRespawn;
	private RealTimeSince _timeSinceDied;

	/// <summary>
	/// Called on the host when the player dies. Starts the respawn countdown so that
	/// PlayerData can trigger a respawn if the PlayerObserver is destroyed (e.g. by cleanup)
	/// before it fires.
	/// </summary>
	public void MarkForRespawn()
	{
		_needsRespawn = true;
		_timeSinceDied = 0;
	}

	/// <summary>
	/// Called by PlayerObserver (owner-only RPC) when the player presses to respawn early,
	/// or by OnUpdate after the timeout. Single entry point for all respawn logic.
	/// </summary>
	[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Reliable )]
	public void RequestRespawn()
	{
		_needsRespawn = false;

		// Clean up any lingering observer for this connection.
		foreach ( var observer in Scene.GetAllComponents<PlayerObserver>().Where( x => x.Network.Owner?.Id == PlayerId ).ToArray() )
		{
			observer.GameObject.Destroy();
		}

		GameManager.Current?.SpawnPlayer( this );
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( !_needsRespawn ) return;
		if ( _timeSinceDied < 4f ) return;

		RequestRespawn();
	}

	[Rpc.Broadcast]
	private void RpcAddStat( string identifier, int amount = 1 )
	{
		Sandbox.Services.Stats.Increment( identifier, amount );
	}

	/// <summary>
	/// Called on the host, calls a RPC on the player and adds a stat
	/// </summary>
	/// <param name="identifier"></param>
	/// <param name="amount"></param>
	public void AddStat( string identifier, int amount = 1 )
	{
		if ( Application.CheatsEnabled ) return;

		Assert.True( Networking.IsHost, "PlayerData.AddStat is host-only!" );

		using ( Rpc.FilterInclude( Connection ) )
		{
			RpcAddStat( identifier, amount );
		}
	}

	void Global.ISaveEvents.AfterLoad( string filename )
	{
		var connection = Connection;
		if ( connection == null )
		{
			// Get new PlayerId from SteamId if this is a new session
			PlayerId = Connection.All.FirstOrDefault( x => x.SteamId == SteamId )?.Id ?? Guid.Empty;
		}
	}
}
