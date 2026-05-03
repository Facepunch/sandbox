using Sandbox.UI;

[Icon( "✨" )]
[Title( "#tool.name.trail" )]
[ClassName( "trail" )]
[Group( "#tool.group.render" )]
public class Trail : ToolMode
{
	[Property, ResourceSelect( Extension = "ldef", AllowPackages = true ), Title( "Line" )]
	public string Definition { get; set; } = "entities/trails/basic.ldef";

	[Property, Sync]
	public Color TrailColor { get; set; } = Color.White;

	[Property, Sync, Range( 0.1f, 128.0f )]
	public float StartWidth { get; set; } = 4.0f;

	[Property, Sync, Range( 0.0f, 128.0f )]
	public float EndWidth { get; set; } = 0.0f;

	[Property, Sync, Range( 0.1f, 10.0f )]
	public float Lifetime { get; set; } = 1.0f;

	[Property, Sync]
	public bool CastShadows { get; set; } = false;

	public override string Description => "Add or remove trails from objects";

	protected override void OnStart()
	{
		base.OnStart();

		RegisterAction( ToolInput.Primary, () => "Add Trail", OnAddTrail );
		RegisterAction( ToolInput.Secondary, () => "Remove Trail", OnRemoveTrail );
	}

	void OnAddTrail()
	{
		var select = TraceSelect();

		IsValidState = select.IsValid() && !select.IsWorld && !select.IsPlayer;
		if ( !IsValidState ) return;

		var lineDef = ResourceLibrary.Get<LineDefinition>( Definition );
		AddTrail( select.GameObject, TrailColor, StartWidth, EndWidth, Lifetime, CastShadows, lineDef );
		ShootEffects( select );
	}

	void OnRemoveTrail()
	{
		var select = TraceSelect();

		IsValidState = select.IsValid() && !select.IsWorld && !select.IsPlayer;
		if ( !IsValidState ) return;

		RemoveTrail( select.GameObject );
		ShootEffects( select );
	}

	[Rpc.Broadcast]
	private void AddTrail( GameObject go, Color color, float startWidth, float endWidth, float lifetime, bool castShadows, LineDefinition lineDef )
	{
		if ( !go.IsValid() ) return;
		if ( go.IsProxy ) return;

		var root = go.Network?.RootGameObject ?? go;

		var existing = root.GetComponent<TrailRenderer>();
		if ( existing.IsValid() )
		{
			existing.Destroy();
		}

		var trail = root.AddComponent<TrailRenderer>();
		trail.Color = new Gradient( new Gradient.ColorFrame( 0, color ), new Gradient.ColorFrame( 1, color.WithAlpha( 0 ) ) );
		trail.Width = new Curve( new Curve.Frame( 0, startWidth ), new Curve.Frame( 1, endWidth ) );
		trail.LifeTime = lifetime;
		trail.CastShadows = castShadows;
		trail.Face = SceneLineObject.FaceMode.Camera;
		trail.Opaque = lineDef.Opaque;
		trail.BlendMode = lineDef.BlendMode;

		if ( lineDef.IsValid() && lineDef.Material.IsValid() )
		{
			trail.Texturing = trail.Texturing with
			{
				Material = lineDef.Material,
				WorldSpace = lineDef.WorldSpace,
				UnitsPerTexture = lineDef.UnitsPerTexture
			};
		}
	}

	[Rpc.Broadcast]
	private void RemoveTrail( GameObject go )
	{
		if ( !go.IsValid() ) return;
		if ( go.IsProxy ) return;

		var root = go.Network?.RootGameObject ?? go;

		var trail = root.GetComponent<TrailRenderer>();
		if ( trail.IsValid() )
		{
			trail.Destroy();
		}
	}
}
