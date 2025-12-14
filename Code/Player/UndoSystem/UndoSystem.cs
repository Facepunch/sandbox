using Sandbox.UI;

public class UndoSystem
{
	Player Player { get; init; }

	Stack<Entry> entries = new Stack<Entry>();

	public UndoSystem( Player player )
	{
		Player = player;
	}

	public Entry Create()
	{
		var entry = new Entry( this );
		entries.Push( entry );
		return entry;
	}

	/// <summary>
	/// Run the undo
	/// </summary>
	public void Undo()
	{
		if ( entries.Count == 0 )
			return;

		var entry = entries.Pop();

		// if we didn't do anything, do the next one
		if ( !entry.Run() )
		{
			Undo();
		}

		// TODO - pop up notice
	}


	public class Entry
	{
		/// <summary>
		/// The name of the undo, should fit the format "Undo something". Like "Undo Spawn Prop".
		/// </summary>
		public string Name { get; set; }
		public string Icon { get; set; }

		UndoSystem System;
		Player Player => System.Player;

		Action actions = null;
		bool actioned;

		internal Entry( UndoSystem system )
		{
			System = system;
		}

		/// <summary>
		/// Add a GameObject that should be destroyed when the undo is undone
		/// </summary>
		public void Add( GameObject go )
		{
			actions += () =>
			{
				if ( go.IsValid() )
				{
					go.Destroy();
					actioned = true;
				}
			};
		}

		/// <summary>
		/// Run this undo
		/// </summary>
		public bool Run( bool sendNotice = true )
		{
			actioned = false;
			actions?.InvokeWithWarning();

			if ( !actioned )
				return false;

			if ( sendNotice )
			{
				using ( Rpc.FilterInclude( Player.Network.Owner ) )
				{
					UndoNotice( Name );
				}
			}

			return true;
		}

		[Rpc.Broadcast]
		public static void UndoNotice( string title )
		{
			Notices.AddNotice( "cached", "#4af", $"Undo {title}".Trim(), 4 );
			Sound.Play( "sounds/ui/ui.undo.sound" );
		}
	}
}
