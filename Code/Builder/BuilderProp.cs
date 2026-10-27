namespace Sandbox;

/// <summary>
/// Recreates the model locally on every peer and after loading a save/dupe.
/// Only the recipe is persistent; runtime model resource names are not portable.
/// </summary>
[Title( "Builder Piece" ), Category( "Game" )]
public sealed class BuilderProp : Component
{
	[Property, Sync( SyncFlags.FromHost )] public string PieceId { get; set; }
	[Property] public ModelRenderer Renderer { get; set; }
	[Property] public ModelCollider Collider { get; set; }
	string builtId;
	int builtGeometryVersion;

	public void Rebuild()
	{
		if ( BuilderCatalog.Find( PieceId ) is not { } piece ) return;
		if ( !Renderer.IsValid() || !Collider.IsValid() ) return;
		var model = BuilderGeometry.GetModel( piece );
		Renderer.Model = model;
		Collider.Model = model;
		builtId = PieceId;
		builtGeometryVersion = BuilderGeometry.GeometryVersion;
	}

	protected override void OnStart() => Rebuild();
	protected override void OnUpdate()
	{
		if ( builtId != PieceId || builtGeometryVersion != BuilderGeometry.GeometryVersion
			|| Renderer.IsValid() && Renderer.Model is null ) Rebuild();
	}
}
