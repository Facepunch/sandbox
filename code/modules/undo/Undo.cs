using Sandbox;
using System;
using System.Collections.Generic;

public class Undo
{
	/// <summary>
	/// The list of objects to be canceled, implemented through the interface - <see langword="ICanUndo"/>.
	/// </summary>
	public static List<UndoEntry> Items = new List<UndoEntry>();

	/// <summary>
	/// Called before the action is undo. The event can be used to cancel a call.
	/// 1. UndoEntry - The class that stores the items to be undo.
	/// 2. string - The name of the block to be undo.
	/// </summary>
	public static Func<UndoEntry, bool> OnUndo { get; set; }

	/// <summary>
	/// Event called after deleting entities from a undo block.
	/// 1. Client - Owner of undo items.
	/// 2. string - The name of the block to be undo.
	/// </summary>
	public static Action<Client, string> OnFinishUndo { get; set; }

	/// <summary>
	/// Adds an item to the undo list.
	/// </summary>
	/// <param name="item">Item undo object</param>
	/// <returns>Returns <see langword="true"/> if added successfully. Otherwise a <see langword="false"/>.</returns>
	public static bool AddItem( UndoEntry item )
	{
		if ( !item.IsValid() )
			return false;

		Items.Add( item );
		return true;
	}

	/// <summary>
	/// Adds an entity to the undo list.
	/// </summary>
	/// <param name="owner">Client owner</param>
	/// <param name="ent">Any entity</param>
	/// <param name="name">Name of the undo action</param>
	/// <returns></returns>
	public static bool AddEntity( Client owner, Entity ent, string name = null )
	{
		var item = new UndoEntry( owner, new EntityUndo( ent ), name );
		return AddItem( item );
	}

	/// <summary>
	/// Adds an entity to the undo list.
	/// </summary>
	/// <param name="owner">The entity from which you want to get the owner</param>
	/// <param name="ent">Any entity</param>
	/// <param name="name">Name of the undo action</param>
	/// <returns></returns>
	public static bool AddEntity( Entity owner, Entity ent, string name = null ) => AddEntity( owner.GetClientOwner(), ent, name );

	/// <summary>
	/// Undoes an action on the index of the list.
	/// </summary>
	/// <param name="index">The index of the item in the undo list</param>
	public static void DoUndo( int index )
	{
		if ( index == -1 )
			return;

		UndoEntry item = Items[index];

		if ( item == null )
			return;

		Client owner = item.GetUndoOwner();
		string Nick = owner.Name;
		string Name = item.GetUndoName();

		bool Result = ( OnUndo != null ) ? OnUndo( item ) : true;

		if ( !Result || !item.DoUndo() )
			return;

		OnFinishUndo?.Invoke( owner, Name );

		Log.Info( $"Player { Nick } undo the { Name }" );

		Items.RemoveAt( index );
	}

	/// <summary>
	/// Undoes the last action for a client.
	/// </summary>
	/// <param name="owner">The client that undo the action</param>
	public static void DoUndo( Client owner )
	{
		if ( owner == null )
			return;

		for ( int i = Items.Count - 1; i >= 0; i-- )
		{
			UndoEntry item = Items[i];

			if ( item != null && item.GetUndoOwner() == owner )
			{
				bool isValid = item.IsValid();

				DoUndo( i );

				if ( isValid )
					break;
			}
		}
	}

	/// <summary>
	/// Undoes all actions for the client.
	/// </summary>
	/// <param name="owner">The client that undo the action</param>
	public static void DoUndoAll( Client owner )
	{
		if ( owner == null )
			return;

		for ( int i = Items.Count - 1; i >= 0; i-- )
		{
			UndoEntry item = Items[i];

			if ( item != null && item.GetUndoOwner() == owner )
				DoUndo( i );
		}
	}

	/// <summary>
	/// Clears the undo list.
	/// </summary>
	public static void ClearItems() => Items.Clear();
}
