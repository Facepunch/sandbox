using Sandbox.UI;

[Icon( "🎈" )]
[Title( "#tool.name.balloon" )]
[ClassName( "balloon" )]
[Group( "#tool.group.building" )]
public class Balloon : ToolMode
{
	public override bool UseSnapGrid => true;
	[Property, ResourceSelect( Extension = "bdef", AllowPackages = true ), Title( "Balloon" )]
	public string Definition { get; set; } = "entities/balloon/basic.bdef";

	[Range( 0, 500 )]
	[Property, Sync]
	public float Length { get; set; } = 50.0f;

	[Range( -10, 10 )]
	[Property, Sync]
	public float Force { get; set; } = 1.0f;

	[Property, Sync]
	public bool Rigid { get; set; } = false;

	[Property, Sync]
	public Color Tint { get; set; } = Color.White;

	public override string Description => "#tool.hint.balloon.description";

	Color _previewTint = Color.Random;

	protected override void OnStart()
	{
		base.OnStart();

		RegisterAction( ToolInput.Primary, () => "#tool.hint.balloon.place_rope", OnPlaceWithRope );
		RegisterAction( ToolInput.Secondary, () => "#tool.hint.balloon.place", OnPlaceWithoutRope );
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();
		_previewTint = Color.Random;
	}

	void OnPlaceWithRope()
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var thrusterDef = ResourceLibrary.Get<BalloonDefinition>( Definition );
		if ( thrusterDef == null ) return;

		var pos = select.WorldTransform();
		var placementTx = new Transform( pos.Position );

		Spawn( select, thrusterDef.Prefab, placementTx, true, _previewTint );
		ShootEffects( select );
		_previewTint = Color.Random;
	}

	void OnPlaceWithoutRope()
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var thrusterDef = ResourceLibrary.Get<BalloonDefinition>( Definition );
		if ( thrusterDef == null ) return;

		var pos = select.WorldTransform();
		var placementTx = new Transform( pos.Position );

		Spawn( select, thrusterDef.Prefab, placementTx, false, _previewTint );
		ShootEffects( select );
		_previewTint = Color.Random;
	}

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var thrusterDef = ResourceLibrary.Get<BalloonDefinition>( Definition );
		if ( thrusterDef == null ) return;

		var pos = select.WorldTransform();
		var placementTx = new Transform( pos.Position );

		var previewTint = Tint == Color.White ? _previewTint : Tint;
		DebugOverlay.GameObject( thrusterDef.Prefab.GetScene(), transform: placementTx, castShadows: true, color: previewTint.WithAlpha( 0.9f ) );
	}

	[Rpc.Host]
	public void Spawn( SelectionPoint point, PrefabFile thrusterPrefab, Transform tx, bool withRope, Color spawnTint )
	{
		var go = thrusterPrefab.GetScene().Clone( global::Transform.Zero, startEnabled: false );
		go.Tags.Add( "removable" );
		go.WorldTransform = Rigid && withRope ? tx.WithPosition( tx.Position + Vector3.Up * Length ) : tx;

		var tint = Tint == Color.White ? spawnTint : Tint;

		foreach ( var c in go.GetComponentsInChildren<Prop>( true ) )
		{
			c.Tint = tint;
		}

		if ( withRope )
		{
			var anchor = new GameObject( false, "anchor" );
			anchor.Parent = point.GameObject;
			anchor.LocalTransform = point.LocalTransform;

			var joint = go.AddComponent<SpringJoint>();
			joint.Body = anchor;
			joint.MinLength = Rigid ? Length : 0;
			joint.MaxLength = Length;
			joint.RestLength = Length;
			joint.Frequency = 0;
			joint.Damping = 0;
			joint.EnableCollision = true;

			var cleanup = go.AddComponent<ConstraintCleanup>();
			cleanup.Attachment = anchor;

			const float ropeWidth = 0.4f;
			var splineInterpolation = 0;
			if ( !Rigid )
			{
				var vertletRope = go.AddComponent<VerletRope>();
				vertletRope.Attachment = anchor;

				const int maxSegmentCount = 48;
				int segmentCount = Math.Min( maxSegmentCount, MathX.CeilToInt( Length / 16.0f ) );

				vertletRope.SegmentCount = segmentCount;
				vertletRope.Radius = ropeWidth;
				splineInterpolation = segmentCount > maxSegmentCount ? 8 : 4;
			}

			var lineRenderer = go.AddComponent<LineRenderer>();
			lineRenderer.Points = [go, anchor];
			lineRenderer.Width = ropeWidth;
			lineRenderer.Color = Color.White;
			lineRenderer.Lighting = true;
			lineRenderer.CastShadows = true;
			lineRenderer.SplineInterpolation = splineInterpolation;
			lineRenderer.Texturing = lineRenderer.Texturing with { Material = Material.Load( "materials/default/rope01.vmat" ), WorldSpace = true, UnitsPerTexture = 32 };
			lineRenderer.Face = SceneLineObject.FaceMode.Cylinder;

			anchor.NetworkSpawn( true, null );
		}

		ApplyPhysicsProperties( go );

		go.NetworkSpawn( true, null );

		foreach ( var c in go.GetComponentsInChildren<Rigidbody>( true ) )
		{
			c.GravityScale = Force;
		}

		var props = go.GetOrAddComponent<PhysicalProperties>();
		props.GravityScale = Force;

		Track( go );

		var undo = Player.Undo.Create();
		undo.Name = "Balloon";
		undo.Add( go );

		Player.PlayerData?.AddStat( "tool.balloon.place" );
	}
}
