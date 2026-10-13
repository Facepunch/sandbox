namespace Sandbox.Bots;

/// <summary>
/// Things a bot can spawn: cloud props, local entities, workshop dupes and tool modes.
/// Loaded once per process.
/// </summary>
public static class BotContent
{
	public static List<string> Props { get; } = new();
	public static List<string> Entities { get; } = new();
	public static List<string> Npcs { get; } = new();
	public static List<string> Weapons { get; } = new();
	public static List<string> Dupes { get; } = new();
	public static List<string> ConstraintTools { get; } = new();
	public static List<string> OtherTools { get; } = new();

	static bool _loading;

	/// <summary>
	/// Props that do something when you mess with them. Spawned more often than the popular list.
	/// </summary>
	public static List<string> Featured { get; } = new();

	static readonly string[] FeaturedQueries = ["explosive", "barrel", "melon", "gas", "propane", "tnt", "balloon", "ragdoll", "vehicle", "glass"];

	public static void EnsureLoaded()
	{
		if ( _loading ) return;
		_loading = true;

		foreach ( var e in ResourceLibrary.GetAll<ScriptedEntity>() )
		{
			var path = e.ResourcePath;
			if ( path.StartsWith( "weapons/" ) ) Weapons.Add( $"entity:{path}" );
			else if ( path.Contains( "/npc/" ) ) Npcs.Add( $"entity:{path}" );
			else Entities.Add( $"entity:{path}" );
		}

		foreach ( var type in Game.TypeLibrary.GetTypes<ToolMode>() )
		{
			if ( type.IsAbstract ) continue;

			if ( type.TargetType.IsAssignableTo( typeof( BaseConstraintToolMode ) ) )
				ConstraintTools.Add( type.ClassName );
			else
				OtherTools.Add( type.ClassName );
		}

		_ = LoadCloud();
	}

	static async Task LoadCloud()
	{
		try
		{
			for ( int page = 0; page < 3; page++ )
			{
				var result = await Package.FindAsync( "sort:popular type:model", 100, page * 100 );
				if ( result?.Packages is null ) break;

				foreach ( var p in result.Packages )
					Props.Add( $"prop:{p.FullIdent}" );
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[bot] cloud props failed: {e.Message}" );
		}

		foreach ( var q in FeaturedQueries )
		{
			try
			{
				var result = await Package.FindAsync( $"sort:popular type:model {q}", 15 );
				if ( result?.Packages is null ) continue;

				foreach ( var p in result.Packages )
					Featured.Add( $"prop:{p.FullIdent}" );
			}
			catch ( Exception e )
			{
				Log.Warning( $"[bot] featured '{q}' failed: {e.Message}" );
			}
		}

		try
		{
			var query = new Storage.Query();
			query.KeyValues["package"] = "facepunch.sandbox";
			query.KeyValues["type"] = "dupe";
			var result = await query.Run();
			for ( int page = 0; page < 3 && result?.Items is not null; page++ )
			{
				foreach ( var item in result.Items )
					Dupes.Add( $"dupe.workshop:{item.Id}" );

				if ( !result.HasMoreResults() ) break;
				result = await result.GetNextResults();
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[bot] workshop dupes failed: {e.Message}" );
		}

		Log.Info( $"[bot] content: {Props.Count} props, {Featured.Count} featured, {Entities.Count} entities, {Npcs.Count} npcs, {Weapons.Count} weapons, {Dupes.Count} dupes, {ConstraintTools.Count + OtherTools.Count} tools" );
	}

	public static string Pick( List<string> list )
	{
		return list.Count > 0 ? Random.Shared.FromList( list ) : null;
	}
}
