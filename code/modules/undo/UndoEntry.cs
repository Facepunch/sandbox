using Sandbox;
using System;
using System.Collections.Generic;

public class UndoEntry
{
	/// <summary>
	/// Owner of undo entities.
	/// </summary>
	private Client undoOwner { get; set; }

	/// <summary>
	/// The name of the block to be undo.
	/// </summary>
	private string undoName { get; set; }

	/// <summary>
	/// Unique identificator. Can be used for calculations in <see langword="OnUndo"/> action.
	/// </summary>
	private string uniqueId { get; set; }

	/// <summary>
	/// List of objects implemented by the interface "ICanUndo" to undo the action.
	/// </summary>
	public List<ICanUndo> objects { get; set; }

	/// <summary>
	/// Called before the action is undo. The event can be used to cancel a call.
	/// 1. UndoEntry - The class that stores the items to be undo.
	/// 2. string - The name of the block to be undo.
	/// </summary>
	public Func<UndoEntry, bool> OnUndo { get; set; }

	/// <summary>
	/// Event called after deleting entities from a undo block.
	/// 1. Client - Owner of undo items.
	/// 2. string - The name of the block to be undo.
	/// </summary>
	public Action<Client, string> OnFinishUndo { get; set; }

	/// <summary>
	/// Undo block constructor.
	/// </summary>
	/// <param name="owner">Client owner</param>
	/// <param name="name">The name for the block to be undo. By default - "Unknown"</param>
	public UndoEntry( Client owner, string name = null )
	{
		undoOwner = owner;
		undoName = (name != null) ? name : "Unknown";
		objects = new List<ICanUndo>();
	}

	/// <summary>
	/// Undo block constructor.
	/// </summary>
	/// <param name="owner">The entity from which you want to get the owner</param>
	/// <param name="name">The name for the block to be undo. By default - "Unknown"</param>
	public UndoEntry( Entity owner, string name = null ) : this( owner.GetClientOwner(), name ) { }

	/// <summary>
	/// Undo block constructor.
	/// </summary>
	/// <param name="owner">Client owner</param>
	/// <param name="item">The item to be undo</param>
	/// <param name="name">The name for the block to be undo. By default - the name of the entity or "Unknown"</param>
	public UndoEntry( Client owner, ICanUndo item, string name = null ) : this( owner, name ) => AddItem( item );

	/// <summary>
	/// Undo block constructor.
	/// </summary>
	/// <param name="owner">The entity from which you want to get the owner</param>
	/// <param name="item">The item to be undo</param>
	/// <param name="name">The name for the block to be undo. By default - the name of the entity or "Unknown"</param>
	public UndoEntry( Entity owner, ICanUndo item, string name = "Unknown" ) : this( owner.GetClientOwner(), item, name ) { }

	/// <summary>
	/// Set a unique identifier for the block to be undo.
	/// </summary>
	/// <param name="uniqueId">Unique identificator</param>
	public void SetUniqueId( string uniqueId ) => this.uniqueId = uniqueId;


	/// <summary>
	/// Add the item to the undo list.
	/// </summary>
	/// <param name="item">Arbitrary item</param>
	/// <returns>Returns <see langword="true"/> if the item was added to the list. Otherwise a <see langword="false"/>.</returns>
	public bool AddItem( ICanUndo item )
	{
		if ( item == null )
			return false;

		objects.Add( item );
		return true;
	}

	/// <summary>
	/// Removes an item from the list if it exists there.
	/// </summary>
	/// <param name="item">An arbitrary item that exists in the list</param>
	/// <returns>Will return <see langword="true"/> if the item has been deleted.  Otherwise a <see langword="false"/>.</returns>
	public bool RemoveItem( ICanUndo item ) => objects.Remove( item );

	/// <summary>
	/// Clears the list of items.
	/// </summary>
	public void RemoveAllItems() => objects.Clear();

	/// <summary>
	/// Undo the action and destroy entities from the list, and also raises events "<see langword="OnUndo"/>" and "<see langword="OnFinishUndo"/>".
	/// </summary>
	public bool DoUndo()
	{
		bool Result = ( OnUndo != null ) ? OnUndo( this ) : true;

		if ( !Result )
			return false;

		foreach ( ICanUndo item in objects )
			if ( item != null && item.IsValidUndo() )
				item.DoUndo();

		OnFinishUndo?.Invoke( undoOwner, undoName );
		return true;
	}

	/// <summary>
	/// Checks the validity of the data of the undone block and nested elements.
	/// </summary>
	/// <returns>Will return <see langword="true"/> if there is an owner and objects that can be undo. Otherwise it's a <see langword="false"/>.</returns>
	public bool IsValid()
	{
		if ( undoOwner == null ) return false;
		if ( objects.Count == 0 ) return false;

		bool IsExistEntities = false;

		foreach ( ICanUndo item in objects )
			if ( item != null && item.IsValidUndo() )
			{
				IsExistEntities = true;
				break;
			}

		return IsExistEntities;
	}

	/// <summary>
	/// Adds itself to the global undo list.
	/// </summary>
	public void Save() => Undo.AddItem( this );

	/// <summary>
	/// Returns the owner of the list of objects.
	/// </summary>
	/// <returns>Owner of undo objects</returns>
	public Client GetUndoOwner() => undoOwner;

	/// <summary>
	/// Returns the name of the block to be undo.
	/// </summary>
	/// <returns>The name of the block to be undo</returns>
	public string GetUndoName() => undoName;

	/// <summary>
	/// Returns the unique identifier of the block to be undo, if any. Can be used for calculations in <see langword="OnUndo"/> action.
	/// </summary>
	/// <returns>Unique identificator</returns>
	public string GetUniqueId() => uniqueId;
}
