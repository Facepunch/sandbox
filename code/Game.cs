using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

[Library( "sandbox", Title = "Sandbox" )]
partial class SandboxGame : Game
{
	public static List<Undo> Undos = new List<Undo>();
	public static List<Redo> Redos = new List<Redo>();
	public static float RedoTime = 5f;

	public SandboxGame()
	{
		if ( IsServer )
		{
			// Create the HUD
			new SandboxHud();
		}
	}

	public override void ClientJoined( Client cl )
	{
		base.ClientJoined( cl );
		var player = new SandboxPlayer();
		player.Respawn();

		cl.Pawn = player;
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
	}

	[ServerCmd( "spawn" )]
	public static void Spawn( string modelname )
	{
		var owner = ConsoleSystem.Caller?.Pawn;

		if ( ConsoleSystem.Caller == null )
			return;

		var tr = Trace.Ray( owner.EyePos, owner.EyePos + owner.EyeRot.Forward * 500 )
			.UseHitboxes()
			.Ignore( owner )
			.Size( 2 )
			.Run();

		var ent = new Prop();
		ent.Position = tr.EndPos;
		ent.Rotation = Rotation.From( new Angles( 0, owner.EyeRot.Angles().yaw, 0 ) ) * Rotation.FromAxis( Vector3.Up, 180 );
		ent.SetModel( modelname );

		Undos.Insert( 0, new Undo( owner, ent ) );

		// Drop to floor
		if ( ent.PhysicsBody != null && ent.PhysicsGroup.BodyCount == 1 )
		{
			var p = ent.PhysicsBody.FindClosestPoint( tr.EndPos );

			var delta = p - tr.EndPos;
			ent.PhysicsBody.Position -= delta;
			//DebugOverlay.Line( p, tr.EndPos, 10, false );
		}

	}

	static readonly SoundEvent HitSound = new( "sounds/balloon_pop_cute.vsnd" )
	{
		Volume = 0.5f,
		DistanceMax = 500.0f
	};

	public static void DoFullUndo( Entity creator, Entity prop, float time, Undo undo, Redo redo )
	{
		if ( !prop.IsValid() ) return;

		OnFullyUndone( creator, prop, time );

		Undos.Remove( undo );
		Redos.Remove( redo );
		prop.Delete();
	}

	public static void DoFullUndo( Entity creator, Entity prop, float time, Undo undo )
	{
		if ( !prop.IsValid() ) return;

		OnFullyUndone( creator, prop, time );

		Undos.Remove( undo );
		prop.Delete();
	}


	[ServerCmd( "undo" )]
	public static async void OnUndo()
	{
		Client client = ConsoleSystem.Caller;

		if ( client == null )
			return;

		Entity pawn = client.Pawn;

		foreach ( Undo undo in Undos )
		{
			Entity creator = undo.Creator;
			Entity prop = undo.Prop;
			float time = undo.Time;

			if ( creator == pawn )
			{
				if ( undo.Avoid ) continue;
				if ( !prop.IsValid() )
				{
					DoFullUndo( creator, prop, time, undo );

					continue;
				}

				using ( Prediction.Off() )
				{
					Particles.Create( "particles/physgun_freeze.vpcf", prop.Position + Vector3.Up * 2 );
					pawn.PlaySound( HitSound.Name );
					OnUndone( creator, prop, time );
				}

				Vector3 Position = prop.Position;
				Rotation Rotation = prop.Rotation;
				Vector3 Velocity = prop.Velocity;
				Redo redo = new Redo( creator, prop, Position, Rotation, Velocity, undo, time );

				Redos.Insert( 0, redo );
				undo.Avoid = true;
				prop.Position = new Vector3(0, 0, -900);

				await prop.Task.DelaySeconds( RedoTime );
					if ( undo.Time + RedoTime < Time.Now )
						DoFullUndo( creator, prop, time, undo, redo );

				break;
			}
		}
	}

	[ClientRpc]
	public static void OnUndone( Entity creator, Entity prop, float time )
	{
		if ( creator == Local.Client.Pawn )
			ChatBox.AddChatEntry( "Undo", "Successfully Moved to Trashbin. Use *redo* to revert.", $"avatar:{Local.SteamId}" );
	}

	[ClientRpc]
	public static void OnFullyUndone( Entity creator, Entity prop, float time )
	{
		if ( creator == Local.Client.Pawn )
			ChatBox.AddChatEntry( "Undo", "Successfully Undone.", $"avatar:{Local.SteamId}" );
	}

	[ServerCmd( "redo" )]
	public static void OnRedo()
	{
		Client client = ConsoleSystem.Caller;

		if ( client == null )
			return;

		Entity pawn = client.Pawn;

		for ( int i = 0; i < Redos.Count; i++ )
		{
			Redo redo = Redos[i];
			Entity creator = redo.Creator;
			Entity prop = redo.Prop;
			Undo undo = redo.Undo;
			float time = redo.Time;

			if ( creator == pawn )
			{
				if ( !prop.IsValid() )
				{
					DoFullUndo( creator, prop, time, undo, redo );

					continue;
				}

				using ( Prediction.Off() )
				{
					Particles.Create( "particles/physgun_freeze.vpcf", prop.Position + Vector3.Up * 2 );
					pawn.PlaySound( HitSound.Name );
					OnRedone( creator, prop, time );
				}

				prop.Position = redo.Pos;
				prop.Rotation = redo.Rotation;
				prop.Velocity = redo.Velocity;
				undo.Avoid = false;
				undo.Time = Time.Now;

				Redos.Remove( redo );

				break;
			}
		}
	}

	[ClientRpc]
	public static void OnRedone( Entity creator, Entity prop, float time )
	{
		if ( creator == Local.Client.Pawn )
			ChatBox.AddChatEntry( "Redo", "Successfully Redone.", $"avatar:{Local.SteamId}" );
	}

	[ServerCmd( "spawn_entity" )]
	public static void SpawnEntity( string entName )
	{
		var owner = ConsoleSystem.Caller.Pawn;

		if ( owner == null )
			return;

		var attribute = Library.GetAttribute( entName );

		if ( attribute == null || !attribute.Spawnable )
			return;

		var tr = Trace.Ray( owner.EyePos, owner.EyePos + owner.EyeRot.Forward * 200 )
			.UseHitboxes()
			.Ignore( owner )
			.Size( 2 )
			.Run();

		var ent = Library.Create<Entity>( entName );
		if ( ent is BaseCarriable && owner.Inventory != null )
		{
			if ( owner.Inventory.Add( ent, true ) )
				return;
		}

		ent.Position = tr.EndPos;
		ent.Rotation = Rotation.From( new Angles( 0, owner.EyeRot.Angles().yaw, 0 ) );

		//Log.Info( $"ent: {ent}" );
	}
}
