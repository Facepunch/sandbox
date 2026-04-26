﻿
using Sandbox.UI;

[Title( "Decal" )]
[Icon( "🖌️" )]
[ClassName( "decaltool" )]
[Group( "Render" )]
public class DecalTool : ToolMode
{
	[Property, ResourceSelect( Extension = "decal", AllowPackages = true ), Title( "Decal" )]
	public string Decal { get; set; }

	public override string Description => "#tool.hint.decaltool.description";

	TimeSince timeSinceShoot = 0;

	protected override void OnStart()
	{
		base.OnStart();

		RegisterAction( ToolInput.Primary, () => "#tool.hint.decaltool.place", OnPlace );
		RegisterAction( ToolInput.Secondary, () => "#tool.hint.decaltool.paint", OnPaint, InputMode.Down );
	}

	void OnPlace()
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var resource = ResourceLibrary.Get<DecalDefinition>( Decal );
		if ( resource == null ) return;

		SpawnDecal( select, resource );
	}

	void OnPaint()
	{
		if ( timeSinceShoot < 0.05f ) return;

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var resource = ResourceLibrary.Get<DecalDefinition>( Decal );
		if ( resource == null ) return;

		timeSinceShoot = 0;
		SpawnDecal( select, resource );
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
