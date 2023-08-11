using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;

namespace Sandbox
{
	partial class Undoer
	{
		private static Dictionary<long, List<Undo>> Undos = new();

		public static Undo Add( IClient creator, Func<string> undoable )
		{
			if ( !Undos.ContainsKey( creator.SteamId ) )
				Undos.Add( creator.SteamId, new List<Undo>() );

			var undo = new Undo( creator )
			{
				Undoable = undoable
			};

			Undos[creator.SteamId].Insert( 0, undo );

			return undo;
		}
		public static Undo Add( IClient creator, Entity prop )
		{
			if ( !Undos.ContainsKey( creator.SteamId ) )
				Undos.Add( creator.SteamId, new List<Undo>() );

			var undo = new Undo( creator, prop );

			Undos[creator.SteamId].Insert( 0, undo );

			return undo;
		}

		public static List<Undo> Get( long id )
		{
			if ( !Undos.ContainsKey( id ) )
				Undos.Add( id, new List<Undo>() );

			return Undos[id];
		}

		public static bool Remove( IClient creator, Undo undo )
		{
			if ( !Undos.ContainsKey( creator.SteamId ) )
				Undos.Add( creator.SteamId, new List<Undo>() );

			return Undos[creator.SteamId].Remove( undo );
		}

		public static void DoUndo( IClient creator, Entity prop, Undo undo, Redo redo = null )
		{
			Undoer.Remove( creator, undo );

			if ( redo != null )
			{
				Redoer.Remove( creator, redo );
			}

			if ( !prop.IsValid() ) return;

			OnUndone( To.Single( creator ) );

			prop.Delete();
		}

		public static void HideProp( Undo undo )
		{
			var prop = undo.Prop;

			prop.EnableDrawing = false;

			if ( prop is ModelEntity modelProp )
			{
				modelProp.EnableAllCollisions = false;
				modelProp.PhysicsEnabled = false; // todo: this isn't ideal for constrained ents (eg. wheels)
			}
		}

		[ClientRpc]
		public static void OnTrashbin()
		{
			// Todo: ChatBox.AddChatEntry seems to do nothing as of 2023
			ChatBox.AddChatEntry( "Undo", "Successfully Moved to Trashbin. Use *redo* to revert.", $"avatar:{Game.LocalClient.SteamId}" );
		}

		[ClientRpc]
		public static void OnUndone()
		{
			ChatBox.AddChatEntry( "Undo", "Successfully Undone.", $"avatar:{Game.LocalClient.SteamId}" );
		}

		[ClientRpc]
		public static void AddUndoPopup( string message )
		{
			ChatBox.AddChatEntry( "Undo", message, $"avatar:{Game.LocalClient.SteamId}" );
		}
	}
}
