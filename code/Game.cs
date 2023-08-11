using System.Threading.Tasks;
using Sandbox;
using System;
using System.Linq;

partial class SandboxGame : GameManager
{
	private SandboxHud _sandboxHud;

	[Event.Hotload]
	public void OnReloaded()
	{
		ReloadManager.ReloadAutoload();
	}

	public SandboxGame()
	{
		Log.Info( "Init SandboxPlus" );
		if ( Game.IsServer )
		{
			Log.Info( "[Server] initting HUD" );
			// Create the HUD
			_sandboxHud = new SandboxHud();
		}

		ReloadManager.ReloadAutoload();
		Event.Run( "game.init" );
	}
	~SandboxGame()
	{
		_sandboxHud?.Delete();
	}

	[Event( "client.join" )]
	public override void ClientJoined( IClient cl )
	{
		base.ClientJoined( cl );
		var player = new SandboxPlayer( cl );

		cl.Pawn = player;

		player.Respawn();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
	}

	// TODO - delete me
	[ConCmd.Admin( "giveall" )]
	public static void GiveAll()
	{
		var player = ConsoleSystem.Caller.Pawn as SandboxPlayer;

		player.Inventory.Add( new Pistol() );
		player.Inventory.Add( new MP5() );
		player.Inventory.Add( new Shotgun() );
		player.Inventory.Add( new RPG() );
	}

	[ConCmd.Server( "spawn" )]
	public static async Task Spawn( string modelname )
	{
		var owner = ConsoleSystem.Caller?.Pawn as Player;

		if ( ConsoleSystem.Caller == null )
			return;

		var tr = Trace.Ray( owner.EyePosition, owner.EyePosition + owner.EyeRotation.Forward * 500 )
			.UseHitboxes()
			.Ignore( owner )
			.Run();

		var modelRotation = Rotation.From( new Angles( 0, owner.EyeRotation.Angles().yaw, 0 ) ) * Rotation.FromAxis( Vector3.Up, 180 );

		//
		// Does this look like a package?
		//
		if ( modelname.Count( x => x == '.' ) == 1 && !modelname.EndsWith( ".vmdl", System.StringComparison.OrdinalIgnoreCase ) && !modelname.EndsWith( ".vmdl_c", System.StringComparison.OrdinalIgnoreCase ) )
		{
			modelname = await SpawnPackageModel( modelname, tr.EndPosition, modelRotation, owner as Entity );
			if ( modelname == null )
				return;
		}

		var model = Model.Load( modelname );
		if ( model == null || model.IsError )
			return;

		var ent = new Prop
		{
			Position = tr.EndPosition + Vector3.Down * model.PhysicsBounds.Mins.z,
			Rotation = modelRotation,
			Model = model
		};

		// Let's make sure physics are ready to go instead of waiting
		ent.SetupPhysicsFromModel( PhysicsMotionType.Dynamic );

		// If there's no physics model, create a simple OBB
		if ( !ent.PhysicsBody.IsValid() )
		{
			ent.SetupPhysicsFromOBB( PhysicsMotionType.Dynamic, ent.CollisionBounds.Mins, ent.CollisionBounds.Maxs );
		}
		Event.Run( "entity.spawned", ent, owner );
	}

	static async Task<string> SpawnPackageModel( string packageName, Vector3 pos, Rotation rotation, Entity source )
	{
		var package = await Package.Fetch( packageName, false );
		if ( package == null || package.PackageType != Package.Type.Model || package.Revision == null )
		{
			// spawn error particles
			return null;
		}

		if ( !source.IsValid ) return null; // source entity died or disconnected or something

		var model = package.GetMeta( "PrimaryAsset", "models/dev/error.vmdl" );
		var mins = package.GetMeta( "RenderMins", Vector3.Zero );
		var maxs = package.GetMeta( "RenderMaxs", Vector3.Zero );

		// downloads if not downloads, mounts if not mounted
		await package.MountAsync();

		return model;
	}

	[ConCmd.Server( "spawn_entity" )]
	public static void SpawnEntity( string entName )
	{
		var owner = ConsoleSystem.Caller.Pawn as Player;

		if ( owner == null )
			return;

		var entityType = TypeLibrary.GetType<Entity>( entName )?.TargetType;
		if ( entityType == null )
			return;

		if ( !TypeLibrary.HasAttribute<SpawnableAttribute>( entityType ) )
			return;

		var tr = Trace.Ray( owner.EyePosition, owner.EyePosition + owner.EyeRotation.Forward * 4096 )
			.UseHitboxes()
			.Ignore( owner )
			.Size( 2 )
			.Run();

		var ent = TypeLibrary.Create<Entity>( entityType );
		if ( ent is BaseCarriable && owner.Inventory != null )
		{
			if ( owner.Inventory.Add( ent, true ) )
				return;
		}

		ent.Position = tr.EndPosition;
		ent.Rotation = Rotation.From( new Angles( 0, owner.EyeRotation.Angles().yaw + 180, 0 ) );

		Event.Run( "entity.spawned", ent, owner );
	}


	[ClientRpc]
	public override void OnKilledMessage( long leftid, string left, long rightid, string right, string method )
	{
		KillFeed.Current?.AddEntry( leftid, left, rightid, right, method );
	}

	[ConCmd.Server( "spawnpackage" )]
	public static async Task SpawnPackage( string fullIdent )
	{
		var owner = ConsoleSystem.Caller.Pawn as Player;

		if ( owner == null )
			return;

		Log.Info( $"Spawn package {fullIdent}" );

		var package = await Package.FetchAsync( fullIdent, false );
		if ( package == null )
		{
			Log.Warning( $"Tried to spawn package {fullIdent} - which was not found" );
			return;
		}

		Log.Info( $"Spawn package {package.Title}" );

		var entityname = package.GetMeta( "PrimaryAsset", "" );

		if ( string.IsNullOrEmpty( entityname ) )
		{
			Log.Warning( $"{package.FullIdent} doesn't have a PrimaryAsset key" );
			return;
		}

		if ( !CanSpawnPackage( package ) )
		{
			Log.Warning( $"Not allowed to spawn package {package.FullIdent}" );
			return;
		}

		await package.MountAsync( true );

		Log.Info( $"Spawning Entity: {entityname}" );

		var type = TypeLibrary.GetType( entityname );
		if ( type == null )
		{
			Log.Warning( $"'{entityname}' type wasn't found for {package.FullIdent}" );
			return;
		}

		Log.Info( $"Found Type: {type.Name}" );
		Log.Info( $"		  : {type.ClassName}" );

		var ent = type.Create<Entity>();

		var tr = Trace.Ray( owner.EyePosition, owner.EyePosition + owner.EyeRotation.Forward * 200 )
							.UseHitboxes()
							.Ignore( owner )
							.Size( 2 )
							.Run();

		ent.Position = tr.EndPosition;
		ent.Rotation = Rotation.From( new Angles( 0, owner.EyeRotation.Angles().yaw, 0 ) );
	}

	static bool CanSpawnPackage( Package package )
	{
		if ( package.PackageType != Package.Type.Addon ) return false;
		if ( !package.Tags.Contains( "runtime" ) ) return false;

		return true;
	}

	[ClientRpc]
	internal static void RespawnEntitiesClient()
	{
		Sandbox.Game.ResetMap( Entity.All.Where( x => !DefaultCleanupFilter( x ) ).ToArray() );
	}

	[ConCmd.Admin( "respawn_entities" )]
	static void RespawnEntities()
	{
		Sandbox.Game.ResetMap( Entity.All.Where( x => !DefaultCleanupFilter( x ) ).ToArray() );
		RespawnEntitiesClient();
	}

	static bool DefaultCleanupFilter( Entity ent )
	{
		// Basic Source engine stuff
		var className = ent.ClassName;
		if ( className == "player" || className == "worldent" || className == "worldspawn" || className == "soundent" || className == "player_manager" )
		{
			return false;
		}

		// When creating entities we only have classNames to work with..
		// The filtered entities below are created through code at runtime, so we don't want to be deleting them
		if ( ent == null || !ent.IsValid ) return true;

		// Gamemode entity
		if ( ent is BaseGameManager ) return false;

		// HUD entities
		if ( ent.GetType().IsBasedOnGenericType( typeof( HudEntity<> ) ) ) return false;

		// Player related stuff, clothing and weapons
		foreach ( var cl in Game.Clients )
		{
			if ( ent.Root == cl.Pawn ) return false;
		}

		// Do not delete view model
		if ( ent is BaseViewModel ) return false;

		return true;
	}
}
