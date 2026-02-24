
using Sandbox.UI;

[Title( "Decal" )]
[Icon( "🖌️" )]
[ClassName( "decaltool" )]
[Group( "Render" )]
public class DecalTool : ToolMode
{
	[Property, ResourceSelect( Extension = "decal", AllowPackages = true ), Title( "Decal" )]
	public string Decal { get; set; }

	public override ToolHint Hint => new( "#tool.hint.decaltool.description", "#tool.hint.decaltool.place", "#tool.hint.decaltool.paint" );

	TimeSince timeSinceShoot = 0;

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var resource = ResourceLibrary.Get<DecalDefinition>( Decal );
		if ( resource == null ) return;

		var def = Decal;
		if ( def == null ) return;

		var pos = select.WorldTransform();

		if ( Input.Pressed( "attack1" ) )
		{
			SpawnDecal( select, resource );
		}

		if ( Input.Down( "attack2" ) && timeSinceShoot > 0.05f )
		{
			timeSinceShoot = 0;
			SpawnDecal( select, resource );
		}
	}

	uint _layer = 0;

	[Rpc.Host]
	public void SpawnDecal( SelectionPoint point, DecalDefinition def )
	{
		if ( def == null ) return;

		var pos = point.WorldTransform();

		var go = new GameObject( true, "decal" );
		go.Tags.Add( "removable" );
		go.WorldPosition = pos.Position + pos.Rotation.Forward * 1f;
		go.WorldRotation = Rotation.LookAt( -pos.Rotation.Forward );
		go.SetParent( point.GameObject, true );

		var decal = go.AddComponent<Decal>();
		decal.Decals = [def];
		decal.SortLayer = _layer++;

		go.NetworkSpawn();

		var undo = Player.Undo.Create();
		undo.Name = "Decal";
		undo.Add( go );
	}
}
