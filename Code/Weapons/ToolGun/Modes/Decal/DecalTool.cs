
using Sandbox.UI;

[Title( "Decal" )]
[Icon( "🖌️" )]
[ClassName( "decaltool" )]
[Group( "Render" )]
public class DecalTool : ToolMode
{
	[Property, ResourceSelect( Extension = "decal", AllowPackages = true ), Title( "Decal" )]
	public string Decal { get; set; }

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

	[Rpc.Host]
	public void SpawnDecal( SelectionPoint point, DecalDefinition def )
	{
		if ( def == null ) return;

		var pos = point.WorldTransform();

		var go = new GameObject( true, "decal" );
		go.Tags.Add( "removable" );
		go.WorldPosition = pos.Position + pos.Rotation.Forward * 1f;
		go.WorldRotation = Rotation.LookAt( -pos.Rotation.Forward );

		var decal = go.AddComponent<Decal>();
		decal.Decals = [def];

		go.NetworkSpawn();

		var undo = Player.Undo.Create();
		undo.Name = "Decal";
		undo.Add( go );
	}
}
