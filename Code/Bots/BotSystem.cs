namespace Sandbox.Bots;

/// <summary>
/// Bot players that act like chaotic sandbox players. The host can add bots
/// (simulated connections), and any instance can hand its own local player to a bot brain.
/// </summary>
public sealed class BotSystem : GameObjectSystem<BotSystem>
{
	public List<BotBrain> Brains { get; } = new();

	[ConVar( "bot_max_objects", Help = "Bots stop spawning once this many objects exist on top of the map. 0 = no limit" )]
	public static int MaxObjects { get; set; } = 0;

	[ConVar( "bot_cleanup_interval", Help = "Host runs a full cleanup every this many seconds. 0 = never" )]
	public static float CleanupInterval { get; set; } = 0;

	public static Dictionary<string, float> Weights { get; } = new()
	{
		["wander"] = 2,
		["noclip"] = 0.4f,
		["spawn_prop"] = 3.5f,
		["spawn_prop_pile"] = 1.5f,
		["spawn_entity"] = 1.5f,
		["spawn_npc"] = 1.8f,
		["spawn_dupe"] = 2,
		["shoot"] = 3.5f,
		["tool_constraint"] = 1.8f,
		["tool_other"] = 1.8f,
		["duplicate"] = 0.6f,
		["physgun"] = 2.5f,
		["gravgun"] = 1,
		["use"] = 0.5f,
		["undo"] = 0.5f,
		["suicide"] = 0.1f,
		["flashlight"] = 0.1f,
	};

	int? _mapObjects;
	TimeSince _timeSinceCleanup;
	TimeSince _timeSinceBotScan;

	public BotSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, -1000, Tick, "Bots" );
		Listen( Stage.StartFixedUpdate, -1000, FixedTick, "Bots" );
	}

	void Tick()
	{
		if ( _timeSinceBotScan > 0.5f )
		{
			_timeSinceBotScan = 0;
			AdoptBots();
		}

		for ( int i = Brains.Count - 1; i >= 0; i-- )
		{
			var brain = Brains[i];

			// Bot left (or was kicked)
			if ( brain.Bot is { IsValid: false } )
			{
				brain.Stop();
				Brains.RemoveAt( i );
				continue;
			}

			try { brain.Tick(); }
			catch ( Exception e ) { Log.Warning( $"[bot] tick {brain.Name}: {e}" ); }
		}

		if ( Networking.IsHost && CleanupInterval > 0 && _timeSinceCleanup > CleanupInterval )
		{
			_timeSinceCleanup = 0;
			ConsoleSystem.Run( "cleanup" );
		}
	}

	void FixedTick()
	{
		foreach ( var brain in Brains )
			brain.FixedUpdate();
	}

	// Every bot gets a brain, however it was added
	void AdoptBots()
	{
		foreach ( var bot in Bot.All )
		{
			if ( Brains.Any( x => x.Bot == bot ) )
				continue;

			BotContent.EnsureLoaded();
			_mapObjects ??= Scene.Directory.Count;
			Brains.Add( new BotBrain( bot ) );
		}
	}

	public static string PickAction()
	{
		var spawned = (Game.ActiveScene?.Directory.Count ?? 0) - (Current?._mapObjects ?? 0);
		var spawnAllowed = MaxObjects <= 0 || spawned < MaxObjects;
		var total = 0f;
		foreach ( var (k, w) in Weights )
			if ( spawnAllowed || !k.StartsWith( "spawn" ) ) total += w;

		var roll = Random.Shared.Float( 0, total );
		foreach ( var (k, w) in Weights )
		{
			if ( !spawnAllowed && k.StartsWith( "spawn" ) ) continue;
			roll -= w;
			if ( roll <= 0 ) return k;
		}

		return "wander";
	}

	static int _botNames;
	static readonly string[] BotNames = ["Gman", "Kleiner", "Alyx", "Barney", "Breen", "Eli", "Mossman", "Grigori", "Vortigaunt", "Odessa", "Magnusson", "Lamarr"];

	/// <summary>
	/// Add or remove bots on the host until there are <paramref name="count"/>.
	/// </summary>
	public void SetBotCount( int count )
	{
		var bots = Bot.All.ToList();
		while ( bots.Count < count )
		{
			var bot = Bot.Create( $"{BotNames[_botNames++ % BotNames.Length]} (bot)" );
			if ( bot is null )
			{
				Log.Warning( "[bot] couldn't add bot - not hosting?" );
				return;
			}

			bots.Add( bot );
		}

		while ( bots.Count > count )
		{
			bots[^1].Remove();
			bots.RemoveAt( bots.Count - 1 );
		}

		AdoptBots();
	}

	/// <summary>
	/// Hand the local player to a bot brain (or take it back).
	/// </summary>
	public void SetLocal( bool enabled )
	{
		BotContent.EnsureLoaded();

		var local = Brains.FirstOrDefault( x => x.Connection is null );
		if ( enabled && local is null )
		{
			_mapObjects ??= Scene.Directory.Count;
			Brains.Add( new BotBrain( null ) );
		}
		else if ( !enabled && local is not null )
		{
			local.Stop();
			Brains.Remove( local );
		}
	}

	[ConCmd( "bot_count", Help = "Host: set the number of bot players" )]
	public static void BotsCommand( int count ) => Current?.SetBotCount( count );

	[ConCmd( "bot_local", Help = "Let a bot brain drive the local player" )]
	public static void LocalCommand( bool enabled )
	{
		if ( Current is null )
			Log.Warning( "[bot] no game is running yet" );

		Current?.SetLocal( enabled );
	}

	[ConCmd( "bot_weight", Help = "Set the weight of a bot action, e.g. bot_weight shoot 5" )]
	public static void WeightCommand( string action, float weight ) => Weights[action] = weight;

	[ConCmd( "bot_status", Help = "What each bot is doing" )]
	public static void StatusCommand()
	{
		foreach ( var brain in Current?.Brains ?? [] )
			Log.Info( $"{brain.Name}: {brain.CurrentAction}" );
	}
}
