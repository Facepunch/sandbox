using Sandbox.UI;

[Hide]
[Title( "#tool.name.hoverball" )]
[Icon( "🎱" )]
[ClassName( "hoverballtool" )]
[Group( "#tool.group.building" )]
public class HoverballTool : ToolMode
{
	public override IEnumerable<string> TraceIgnoreTags => ["constraint", "collision"];

	[Property, ResourceSelect( Extension = "hdef", AllowPackages = true ), Title( "Hoverball" )]
	public string Definition { get; set; } = "entities/hoverball/basic.hdef";

	public override string Description => "#tool.hint.hoverballtool.description";

	public override bool UseSnapGrid => true;

	protected override void OnStart()
	{
		base.OnStart();

		RegisterAction( ToolInput.Primary, () => "#tool.hint.hoverballtool.place", OnPlace );
	}

	void OnPlace()
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var hoverballDef = ResourceLibrary.Get<HoverballDefinition>( Definition );
		if ( hoverballDef == null ) return;

		var pos = select.WorldTransform();
		var placementTrans = new Transform( pos.Position );

		Spawn( select, hoverballDef.Prefab, placementTrans );
		ShootEffects( select );
	}

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var hoverballDef = ResourceLibrary.Get<HoverballDefinition>( Definition );
		if ( hoverballDef == null ) return;

		var pos = select.WorldTransform();
		var placementTrans = new Transform( pos.Position );

		DebugOverlay.GameObject( hoverballDef.Prefab.GetScene(), transform: placementTrans, castShadows: true, color: Color.White.WithAlpha( 0.9f ) );
	}

	[Rpc.Host]
	public void Spawn( SelectionPoint point, PrefabFile hoverballPrefab, Transform tx )
	{
		if ( hoverballPrefab == null )
			return;

		var go = hoverballPrefab.GetScene().Clone( global::Transform.Zero, startEnabled: false );
		go.Tags.Add( "removable" );
		go.Tags.Add( "constraint" );
		go.WorldTransform = tx;

		if ( !point.IsWorld )
		{
			var hoverball = go.GetComponent<HoverballEntity>( true );

			var joint = hoverball.AddComponent<FixedJoint>();
			joint.Attachment = Joint.AttachmentMode.LocalFrames;
			joint.LocalFrame2 = point.GameObject.WorldTransform.WithScale( 1 ).ToLocal( tx );
			joint.LocalFrame1 = new Transform();
			joint.AngularFrequency = 0;
			joint.LinearFrequency = 0;
			joint.Body = point.GameObject;
			joint.EnableCollision = false;
		}

		ApplyPhysicsProperties( go );

		go.NetworkSpawn( true, null );

		Track( go );

		var undo = Player.Undo.Create();
		undo.Name = "Hoverball";
		undo.Icon = "🎱";
		undo.Add( go );

		CheckContraptionStats( point.GameObject );
	}
}
