public sealed partial class Player
{
	/// <summary>
	/// Find a player for this connection
	/// </summary>
	public static Player FindForConnection( Connection c )
	{
		return Game.ActiveScene.GetAll<Player>().FirstOrDefault( x => x.Network.Owner == c );
	}

	/// <summary>
	/// Get player from a connecction id
	/// </summary>
	/// <param name="playerId"></param>
	/// <returns></returns>
	public static Player For( Guid playerId )
	{
		return Game.ActiveScene.GetAll<Player>().FirstOrDefault( x => x.PlayerId.Equals( playerId ) );
	}

	/// <summary>
	/// Kill yourself
	/// </summary>
	[ConCmd( "kill" )]
	public static void KillSelf( Connection source )
	{
		var player = Player.FindForConnection( source );
		if ( player is null ) return;

		player.KillSelf();
	}

	[Rpc.Host]
	internal void KillSelf()
	{
		if ( Rpc.Caller != Network.Owner ) return;

		this.OnDamage( new DamageInfo( float.MaxValue, GameObject, null ) );
	}

	[ConCmd( "god", ConVarFlags.Server | ConVarFlags.Cheat, Help = "Toggle invulnerability" )]
	public static void God( Connection source )
	{
		var player = PlayerData.For( source );
		if ( !player.IsValid() )
			return;

		player.IsGodMode = !player.IsGodMode;
		source.SendLog( LogLevel.Info, player.IsGodMode ? "Godmode enabled" : "Godmode disabled" );
	}

	/// <summary>
	/// Switch to another map
	/// </summary>
	[ConCmd( "map", ConVarFlags.Admin )]
	public static void ChangeMap( string mapName )
	{
		LaunchArguments.Map = mapName;
		Game.Load( Game.Ident, true );
	}

	/// <summary>
	/// Switch to another map
	/// </summary>
	[ConCmd( "undo", ConVarFlags.Server )]
	public static void RunUndo( Connection source )
	{
		var player = Player.FindForConnection( source );
		if ( !player.IsValid() )
			return;

		player.Undo.Undo();
	}
}
