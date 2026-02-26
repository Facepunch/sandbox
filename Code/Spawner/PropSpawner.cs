/// <summary>
/// Payload for spawning a prop model from a cloud ident.
/// </summary>
public class PropSpawner : ISpawner
{
	public string DisplayName { get; private set; }
	public string Icon => Path;
	public string Data => Path;
	public BBox Bounds => Model?.Bounds ?? default;
	public bool IsReady => Model is not null && !Model.IsError;

	public Model Model { get; private set; }
	public string Path { get; }

	public PropSpawner( string path )
	{
		Path = path;
		DisplayName = System.IO.Path.GetFileNameWithoutExtension( path );
		_ = LoadAsync();
	}

	private async Task LoadAsync()
	{
		Model = await Cloud.Load<Model>( Path );
		if ( Model is not null )
		{
			DisplayName = Model.Name ?? DisplayName;
		}
	}

	public void DrawPreview( Transform transform, Material overrideMaterial )
	{
		if ( !IsReady ) return;

		Game.ActiveScene.DebugOverlay.Model( Model, transform: transform, overlay: false, materialOveride: overrideMaterial );
	}

	public Task<List<GameObject>> Spawn( Transform transform, Player player )
	{
		var depth = -Bounds.Mins.z;
		transform.Position += transform.Up * depth;

		var go = new GameObject( false, "prop" );
		go.Tags.Add( "removable" );
		go.WorldTransform = transform;

		var prop = go.AddComponent<Prop>();
		prop.Model = Model;

		Ownable.Set( go, player.Network.Owner );

		if ( (Model.Physics?.Parts?.Count ?? 0) == 0 )
		{
			var collider = go.AddComponent<BoxCollider>();
			collider.Scale = Model.Bounds.Size;
			collider.Center = Model.Bounds.Center;
			go.AddComponent<Rigidbody>();
		}

		go.NetworkSpawn( true, null );

		return Task.FromResult( new List<GameObject> { go } );
	}
}
