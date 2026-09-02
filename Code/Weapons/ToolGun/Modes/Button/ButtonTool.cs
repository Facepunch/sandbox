using Sandbox.UI;

[Hide]
[Title( "#tool.name.button" )]
[Icon( "🔘" )]
[ClassName( "buttontool" )]
[Group( "#tool.group.building" )]
public sealed class ButtonTool : ToolMode
{
	public override bool UseSnapGrid => true;
	public override IEnumerable<string> TraceIgnoreTags => ["constraint", "collision"];

	[Property, ResourceSelect( Extension = "btndef", AllowPackages = true ), Title( "Button" )]
	public string Definition { get; set; } = "entities/button/basic.btndef";

	public override string Description => "#tool.hint.buttontool.description";

	protected override void OnStart()
	{
		base.OnStart();
		RegisterAction( ToolInput.Primary, () => "#tool.hint.buttontool.place", OnPlace );
		RegisterAction( ToolInput.Secondary, () => "#tool.hint.buttontool.place_no_weld", OnPlaceNoWeld );
	}

	void OnPlace() => Place( noWeld: false );
	void OnPlaceNoWeld() => Place( noWeld: true );

	void Place( bool noWeld )
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var definition = ResourceLibrary.Get<ButtonDefinition>( Definition );
		if ( definition?.Prefab is null ) return;

		var transform = GetPlacementTransform( select );
		Spawn( select, definition.Prefab, transform, noWeld );
		ShootEffects( select );
	}

	Transform GetPlacementTransform( SelectionPoint select )
	{
		var surface = select.WorldTransform();
		return new Transform( surface.Position, surface.Rotation );
	}

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var definition = ResourceLibrary.Get<ButtonDefinition>( Definition );
		if ( definition?.Prefab?.GetScene() is not Scene scene ) return;

		DebugOverlay.GameObject( scene, transform: GetPlacementTransform( select ), castShadows: true, color: Color.White.WithAlpha( 0.9f ) );
	}

	[Rpc.Host]
	public void Spawn( SelectionPoint point, PrefabFile buttonPrefab, Transform transform, bool noWeld )
	{
		if ( buttonPrefab?.GetScene() is not Scene scene ) return;

		var button = scene.Clone( new CloneConfig { StartEnabled = false } );
		button.Tags.Add( "removable" );
		button.Tags.Add( "constraint" );
		button.WorldTransform = transform;

		if ( !noWeld )
		{
			var joint = button.AddComponent<FixedJoint>();
			joint.Attachment = Joint.AttachmentMode.LocalFrames;
			joint.LocalFrame2 = point.GameObject.WorldTransform.WithScale( 1 ).ToLocal( transform );
			joint.LocalFrame1 = new Transform();
			joint.AngularFrequency = 0;
			joint.LinearFrequency = 0;
			joint.Body = point.GameObject;
			joint.EnableCollision = false;
		}

		ApplyPhysicsProperties( button );
		button.NetworkSpawn( true, null );
		Track( button );

		var undo = Player.Undo.Create();
		undo.Name = "Button";
		undo.Icon = "🔘";
		undo.Add( button );

		CheckContraptionStats( point.GameObject );
	}
}
