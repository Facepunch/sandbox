using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Payload for spawning a duplicator contraption.
/// </summary>
public class DuplicatorSpawner : ISpawner
{
	public string DisplayName { get; private set; } = "Duplication";
	public string Icon => null;
	public string Data => Json;
	public BBox Bounds => Dupe?.Bounds ?? default;
	public bool IsReady => Dupe is not null && _packagesReady;

	public DuplicationData Dupe { get; }

	public string Json { get; }

	private bool _packagesReady;

	public DuplicatorSpawner( DuplicationData dupe, string json, string name = null )
	{
		Dupe = dupe;
		Json = json;
		DisplayName = name ?? "Duplication";
		_ = InstallPackages();
	}

	/// <summary>
	/// Create from raw JSON (e.g. from a storage entry).
	/// </summary>
	public static DuplicatorSpawner FromJson( string json, string name = null )
	{
		var dupe = Sandbox.Json.Deserialize<DuplicationData>( json );
		return new DuplicatorSpawner( dupe, json, name );
	}

	private async Task InstallPackages()
	{
		if ( Dupe?.Packages is null || Dupe.Packages.Count == 0 )
		{
			_packagesReady = true;
			return;
		}

		foreach ( var pkg in Dupe.Packages )
		{
			if ( Cloud.IsInstalled( pkg ) )
				continue;

			await Cloud.Load( pkg );
		}

		_packagesReady = true;
	}

	public void DrawPreview( Transform transform, Material overrideMaterial )
	{
		if ( Dupe is null ) return;

		foreach ( var model in Dupe.PreviewModels )
		{
			if ( model.Model.IsError )
			{
				var bounds = model.Bounds;
				if ( bounds.Size.IsNearlyZero() ) continue;

				var t = transform.ToWorld( model.Transform );
				t = new Transform( t.PointToWorld( bounds.Center ), t.Rotation, t.Scale * (bounds.Size / 50) );
				Game.ActiveScene.DebugOverlay.Model( Model.Cube, transform: t, overlay: false, materialOveride: overrideMaterial );
			}
			else
			{
				Game.ActiveScene.DebugOverlay.Model( model.Model, transform: transform.ToWorld( model.Transform ), overlay: false, materialOveride: overrideMaterial, localBoneTransforms: model.Bones );
			}
		}
	}

	public Task<List<GameObject>> Spawn( Transform transform, Player player )
	{
		var jsonObject = Sandbox.Json.ToNode( Dupe ) as JsonObject;
		SceneUtility.MakeIdGuidsUnique( jsonObject );

		var results = new List<GameObject>();

		using ( Game.ActiveScene.BatchGroup() )
		{
			foreach ( var entry in jsonObject["Objects"] as JsonArray )
			{
				if ( entry is not JsonObject obj )
					continue;

				var pos = entry["Position"]?.Deserialize<Vector3>() ?? default;
				var rot = entry["Rotation"]?.Deserialize<Rotation>() ?? Rotation.Identity;
				var scl = entry["Scale"]?.Deserialize<Vector3>() ?? Vector3.One;

				var world = transform.ToWorld( new Transform( pos, rot ) );
				world.Scale = scl;

				var go = new GameObject( false );
				go.Deserialize( obj, new GameObject.DeserializeOptions { TransformOverride = world } );

				Ownable.Set( go, player.Network.Owner );
				go.NetworkSpawn( true, null );

				results.Add( go );
			}
		}

		return Task.FromResult( results );
	}
}
