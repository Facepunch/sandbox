/// <summary>
/// Describes something that can be spawned into the world
/// Implementations handle their own preview rendering and spawn logic.
/// </summary>
public interface ISpawner
{
	/// <summary>
	/// Display name shown in the HUD while holding this payload.
	/// </summary>
	string DisplayName { get; }

	/// <summary>
	/// Icon path for this payload, used for inventory display via <c>thumb:path</c>.
	/// </summary>
	string Icon { get; }

	/// <summary>
	/// The local-space bounds of the thing being spawned, used to offset placement so it sits on surfaces.
	/// </summary>
	BBox Bounds { get; }

	/// <summary>
	/// Whether all required resources (packages, models, etc.) are loaded and ready to place.
	/// </summary>
	bool IsReady { get; }

	/// <summary>
	/// Serialize this payload to a string that can be synced over the network.
	/// Format is <c>type:data</c>, e.g. <c>prop:facepunch.post_box</c>.
	/// </summary>
	string Serialize();

	/// <summary>
	/// Draw a ghost preview at the given world transform.
	/// </summary>
	void DrawPreview( Transform transform, Material overrideMaterial );

	/// <summary>
	/// Actually spawn the thing at the given transform. Called on the host.
	/// Returns the root GameObject(s) that were spawned so they can be added to undo.
	/// </summary>
	Task<List<GameObject>> Spawn( Transform transform, Player player );

	/// <summary>
	/// Reconstruct an <see cref="ISpawner"/> from a serialized string.
	/// </summary>
	static ISpawner Deserialize( string data )
	{
		if ( string.IsNullOrWhiteSpace( data ) )
			return null;

		var colonIndex = data.IndexOf( ':' );
		if ( colonIndex < 0 )
			return null;

		var type = data[..colonIndex];
		var value = data[(colonIndex + 1)..];

		return type switch
		{
			"prop" => new PropSpawner( value ),
			"entity" => new EntitySpawner( value ),
			"dupe" => DuplicatorSpawner.FromJson( value ),
			_ => null
		};
	}
}
