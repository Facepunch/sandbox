/// <summary>
/// Persists morph values on a GameObject so they survive over the network and dupes. 
/// </summary>
[Title( "Morph State" )]
[Category( "Rendering" )]
public sealed class MorphState : Component
{
	/// <summary>
	/// Serialized morphs. Sync'd to clients
	/// </summary>
	[Property, Sync, Change( nameof( Apply ) )]
	public string SerializedMorphs { get; set; }

	protected override void OnStart()
	{
		Apply();
	}

	/// <summary>
	/// Snapshot all current morph values from <paramref name="smr"/> into <see cref="SerializedMorphs"/>.
	/// Call this on the host after changing any morph value.
	/// </summary>
	public void Capture( SkinnedModelRenderer smr )
	{
		SerializedMorphs = Json.Serialize( smr.Morphs.Names.ToDictionary( n => n, n => smr.SceneModel?.Morphs.Get( n ) ?? 0f ) );
	}

	/// <summary>
	/// Apply the stored <see cref="SerializedMorphs"/> to the first <see cref="SkinnedModelRenderer"/> we find
	/// </summary>
	public void Apply()
	{
		if ( string.IsNullOrEmpty( SerializedMorphs ) ) return;

		var smr = GameObject.GetComponentInChildren<SkinnedModelRenderer>();
		if ( !smr.IsValid() ) return;

		var morphs = Json.Deserialize<Dictionary<string, float>>( SerializedMorphs );
		if ( morphs is null ) return;

		foreach ( var name in smr.Morphs.Names )
		{
			smr.Morphs.Clear( name );
		}

		foreach ( var (name, val) in morphs )
		{
			smr.Morphs.Set( name, val );
		}
	}
}
