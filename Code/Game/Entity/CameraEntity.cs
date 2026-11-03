/// <summary>A standalone camera entity with its own view and TV feed, not an inventory item.</summary>
[Title( "#entity.name.camera" ), Icon( "videocam" )]
[SignalOutput( Default = true )]
public sealed class CameraEntity : Component, ICameraModifier, IPhysgunEvent, ICarryEvent
{
	[Property, ClientEditable, Title( "#tool.setting.camera_key" ), Description( "#tool.setting.camera_key.description" ), Group( "#tool.setting.group.inputs" )]
	public ClientInput ViewInput { get; set; }

	[Property, ClientEditable, Range( 1, 150 ), Title( "#tool.setting.camera_fov" ), Group( "#tool.setting.group.behavior" )]
	public float Fov { get; set; } = 75f;

	[Property, ClientEditable, Title( "#tool.setting.camera_static" ), Description( "#tool.setting.camera_static.description" ), Group( "#tool.setting.group.behavior" )]
	public bool Static { get; set; }

	[Property, ClientEditable, Title( "#tool.setting.camera_toggle" ), Description( "#tool.setting.camera_toggle.description" ), Group( "#tool.setting.group.inputs" )]
	public bool ToggleMode { get; set; } = true;

	[Property, Hide] public CameraComponent Lens { get; set; }
	public Transform ViewTransform => Lens.IsValid() ? Lens.WorldTransform : WorldTransform;
	public float ClampedFov => float.IsFinite( Fov ) ? Fov.Clamp( 1f, 150f ) : 75f;
	internal long ViewOrder { get; private set; }
	static long _nextViewOrder;
	bool? _appliedStatic;
	Texture _renderTexture;

	/// <summary>Allocated only when a TV requests the feed.</summary>
	public Texture RenderTexture
	{
		get
		{
			if ( !Active || !Lens.IsValid() ) return null;
			if ( !_renderTexture.IsValid() )
				_renderTexture = Texture.CreateRenderTarget().WithSize( 512, 512 ).Create();
			Lens.RenderTarget = _renderTexture;
			return _renderTexture;
		}
	}

	public bool IsLocallyOwned => GetComponent<Ownable>()?.Owner is { } owner && owner == Connection.Local;

	protected override void OnEnabled()
	{
		ViewOrder = ++_nextViewOrder;
		Lens ??= GetComponentInChildren<CameraComponent>( true );
		if ( Lens.IsValid() )
		{
			Lens.IsMainCamera = false;
			Lens.BackgroundColor = Color.Black;
			Lens.ClearFlags = ClearFlags.Color | ClearFlags.Depth | ClearFlags.Stencil;
			Lens.RenderExcludeTags.Add( "viewmodel" );
		}
		RefreshPhysics();
		if ( Scene == Game.ActiveScene ) CameraViewSystem.GetOrCreate( Scene );
	}

	protected override void OnUpdate()
	{
		if ( Scene == Game.ActiveScene ) CameraViewSystem.GetOrCreate( Scene );
		var body = GetComponent<Rigidbody>( true );
		var collider = GetComponent<ModelCollider>( true );
		if ( _appliedStatic != Static || Static && (body.IsValid() && (body.Enabled || body.MotionEnabled)
			|| collider.IsValid() && !collider.Static) ) RefreshPhysics();
	}

	protected override void OnPreRender()
	{
		if ( !Lens.IsValid() ) return;
		Lens.FieldOfView = ClampedFov;
		Lens.FovAxis = CameraComponent.Axis.Horizontal;
		if ( _renderTexture.IsValid() ) Lens.RenderTarget = _renderTexture;
	}

	void CleanupFeed()
	{
		if ( Lens.IsValid() ) Lens.RenderTarget = null;
		_renderTexture?.Dispose();
		_renderTexture = null;
	}

	public void RefreshPhysics()
	{
		var collider = GetComponent<ModelCollider>( true );
		var body = GetComponent<Rigidbody>( true );
		if ( collider.IsValid() ) collider.Static = Static;
		if ( body.IsValid() )
		{
			if ( Static )
			{
				body.MotionEnabled = false;
				body.Enabled = false;
			}
			else if ( _appliedStatic == true || !body.Enabled )
			{
				body.Enabled = true;
				body.MotionEnabled = false;
			}
		}
		_appliedStatic = Static;
	}

	/// <summary>Align the lens, rather than the model origin, with the captured player view.</summary>
	public void AlignView( Transform view )
	{
		Lens ??= GetComponentInChildren<CameraComponent>( true );
		var local = Lens.IsValid() ? WorldTransform.ToLocal( Lens.WorldTransform ) : global::Transform.Zero;
		var rotation = view.Rotation * local.Rotation.Inverse;
		WorldTransform = WorldTransform.WithPosition( view.Position - rotation * (local.Position * WorldScale) ).WithRotation( rotation );
	}

	int ICameraModifier.CameraOrder => 1000;
	void ICameraModifier.ModifyCamera( CameraComponent camera, ref CameraView view )
	{
		if ( Scene != Game.ActiveScene || camera != Scene.Camera ) return;
		CameraViewSystem.Get( Scene )?.ApplyView( this, camera, ref view );
	}

	void IPhysgunEvent.OnPhysgunGrab( IPhysgunEvent.GrabEvent e )
	{
		if ( Static ) e.Cancelled = true;
	}

	void ICarryEvent.OnCarryGrab( PlayerCarryEvent e )
	{
		if ( Static ) e.Cancelled = true;
	}

	protected override void OnDisabled()
	{
		CleanupFeed();
		if ( Scene == Game.ActiveScene ) CameraViewSystem.Get( Scene )?.Release( this );
	}

	protected override void OnDestroy()
	{
		CleanupFeed();
		if ( Scene == Game.ActiveScene ) CameraViewSystem.Get( Scene )?.Release( this );
	}
}
