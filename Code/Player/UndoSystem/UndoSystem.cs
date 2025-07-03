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
		entry.Run();

		// TODO - pop up notice
	}


	public class Entry
	{
		UndoSystem System;
		Player Player => System.Player;

		Action actions = null;

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
				}
			};
		}

		/// <summary>
		/// Run this undo
		/// </summary>
		public void Run()
		{
			actions?.InvokeWithWarning();
		}
	}
}
