using Sandbox.UI;
using System.Collections.Generic;
using System.Linq;

public class UndoSystem : GameObjectSystem<UndoSystem>
{
	Dictionary<long, PlayerStack> stacks = new();

	public UndoSystem( Scene scene ) : base( scene )
	{
	}

	/// <summary>
	/// Get the undo stack for a specific SteamId
	/// </summary>
	public PlayerStack For( long steamId )
	{
		if ( !stacks.TryGetValue( steamId, out var stack ) )
		{
			stack = new PlayerStack( steamId );
			stacks[steamId] = stack;
		}
		return stack;
	}

	/// <summary>
	/// Call this when a player disconnects to prevent memory leaks!
	/// </summary>
	public void RemovePlayer( long steamId )
	{
		stacks.Remove( steamId );
	}

	/// <summary>
	/// Remove a GameObject from all player undo stacks.
	/// </summary>
	public void Remove( GameObject go )
	{
		foreach ( var stack in stacks.Values )
		{
			stack.Remove( go );
		}
	}

	/// <summary>
	/// Per-player undo stack
	/// </summary>
	public class PlayerStack
	{
		long steamId;
		List<Entry> entries = new();
		const int MaxUndoSteps = 128; // Bounded history prevents indefinite memory leaks

		public PlayerStack( long steamId )
		{
			this.steamId = steamId;
		}

		/// <summary>
		/// Create a new undo entry
		/// </summary>
		public Entry Create()
		{
			var entry = new Entry( steamId );
			entries.Add( entry );

			if ( entries.Count > MaxUndoSteps )
			{
				entries.RemoveAt( 0 );
			}

			return entry;
		}

		/// <summary>
		/// Run the undo
		/// </summary>
		public void Undo()
		{
			while ( entries.Count > 0 )
			{
				var entry = entries[^1];
				entries.RemoveAt( entries.Count - 1 );

				if ( entry.Run() )
					return;
			}
		}

		/// <summary>
		/// Remove a GameObject from all entries in this stack.
		/// </summary>
		public void Remove( GameObject go )
		{
			foreach ( var entry in entries )
				entry.Remove( go );
		}
	}

	/// <summary>
	/// An undo entry
	/// </summary>
	public class Entry
	{
		/// <summary>
		/// The name of the undo, should fit the format "Undo something". Like "Undo Spawn Prop".
		/// </summary>
		public string Name { get; set; }
		public string Icon { get; set; }

		long SteamId;

		HashSet<GameObject> gameObjects = new();

		internal Entry( long steamId )
		{
			SteamId = steamId;
		}

		/// <summary>
		/// Add a GameObject that should be destroyed when the undo is undone
		/// </summary>
		public void Add( GameObject go )
		{
			gameObjects.Add( go );
		}

		/// <summary>
		/// Add a collection of GameObjects that should be destroyed when the undo is undone
		/// </summary>
		/// <param name="gos"></param>
		public void Add( params IEnumerable<GameObject> gos )
		{
			foreach ( var go in gos )
			{
				Add( go );
			}
		}

		/// <summary>
		/// Remove a GameObject from this entry so it will no longer be destroyed on undo.
		/// </summary>
		public void Remove( GameObject go )
		{
			gameObjects.Remove( go );
		}

		/// <summary>
		/// Run this undo
		/// </summary>
		public bool Run( bool sendNotice = true )
		{
			var actioned = false;

			foreach ( var go in gameObjects )
			{
				if ( go.IsValid() )
				{
					go.Destroy();
					actioned = true;
				}
			}

			if ( !actioned )
				return false;

			if ( sendNotice )
			{
				var c = Connection.All.FirstOrDefault( x => x.SteamId == SteamId );
				if ( c is not null )
				{
					using ( Rpc.FilterInclude( c ) )
					{
						UndoNotice( Name );
					}
				}
			}

			return true;
		}

		[Rpc.Broadcast]
		public static void UndoNotice( string title )
		{
			Notices.AddNotice( "cached", "#3273eb", $"Undo {title}".Trim(), 5 );
			Sound.Play( "sounds/ui/ui.undo.sound" );
		}
	}
}
