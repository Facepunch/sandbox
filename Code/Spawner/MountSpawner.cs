using Sandbox.Mounting;

/// <summary>
/// Like <see cref="PropSpawner"/>, but attaches <see cref="MountMetadata"/> to the spawned object
/// so clients without the mount installed can show a fallback cube and install prompt.
/// </summary>
public sealed class MountSpawner : ISpawner
{
	record Metadata( string GameTitle );

	public string DisplayName { get; private set; }
	public string Icon => Path;
	public string Data => Path;
	public BBox Bounds => Model.IsValid() ? Model.Bounds : default;
	public bool IsReady => Model.IsValid();
	public Task<bool> Loading { get; }

	public Model Model { get; private set; }
	public string Path { get; }
	public string GameTitle { get; private set; }

	public MountSpawner( string path )
	{
		Path = path;

		if ( MountUtility.TryParse( path, out string sourceName ) )
		{
			var source = Directory.Get( sourceName );
			if ( source is null )
			{
				Log.Warning( $"Couldn't find source \"{sourceName}\"" );
			}
			else
			{
				GameTitle = source.Title;
			}
		}

		DisplayName = System.IO.Path.GetFileNameWithoutExtension( path );
		Loading = LoadAsync();
	}

	private async Task<bool> LoadAsync()
	{
		Model = await ResourceLibrary.LoadAsync<Model>( Path );
		Log.Info( $"[MountSpawner] path='{Path}' model={(Model.IsValid() ? "loaded" : "missing")} title='{GameTitle}'" );
		return true; // missing model uses placeholder
	}

	/// <summary>Serialize mount metadata to pass through the Spawn RPC.</summary>
	public static string SerializeMetadata( string gameTitle )
		=> Json.Serialize( new Metadata( gameTitle ) );

	public void DrawPreview( Transform transform, Material overrideMaterial )
	{
		var bounds = Bounds;
		var t = transform;
		t = new Transform( t.PointToWorld( bounds.Center ), t.Rotation, t.Scale * ( bounds.Size / 50f ) );
		Game.ActiveScene.DebugOverlay.Model( Model.IsValid() ? Model : Model.Cube, transform: t, overlay: false, materialOveride: overrideMaterial );
	}

	public Task<List<GameObject>> Spawn( Transform transform, Player player )
	{
		var effectiveBounds = Model.IsValid() ? Model.Bounds : new BBox( -Vector3.One * 8f, Vector3.One * 8f );
		var depth = Model.IsValid() ? -Model.Bounds.Mins.z : effectiveBounds.Size.z / 2f;
		transform.Position += transform.Up * depth;

		var go = new GameObject( false, "prop" );
		go.Tags.Add( "removable" );
		go.WorldTransform = transform;

		if ( Model.IsValid() )
		{
			var prop = go.AddComponent<Prop>();
			prop.Model = Model;

			if ( (Model.Physics?.Parts?.Count ?? 0) == 0 )
			{
				var collider = go.AddComponent<BoxCollider>();
				collider.Scale = Model.Bounds.Size;
				collider.Center = Model.Bounds.Center;
				go.AddComponent<Rigidbody>();
			}
		}
		else
		{
			var collider = go.AddComponent<BoxCollider>();
			collider.Scale = effectiveBounds.Size;
			collider.Center = effectiveBounds.Center;
			go.AddComponent<Rigidbody>();
		}

		var meta = go.AddComponent<MountMetadata>();
		meta.GameTitle = GameTitle;
		meta.BoundsSize = effectiveBounds.Size;
		meta.BoundsCenter = effectiveBounds.Center;

		Ownable.Set( go, player.Network.Owner );
		go.NetworkSpawn( true, null );

		return Task.FromResult( new List<GameObject> { go } );
	}
}
