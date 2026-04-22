/// <summary>
/// A TV screen entity that displays the feed from a linked <see cref="CameraEntity"/>.
/// Use the Linker tool to connect a Camera to this TV.
/// </summary>
public class TVEntity : Component
{
	[Property]
	public string ScreenMaterialName { get; set; } = "screen";

	[Property, Range( 0.5f, 10 ), Step( 0.5f ), ClientEditable, Group( "Screen" )]
	public float Brightness { get; set; } = 1f;

	[Property, ClientEditable, Group( "Screen" )]
	public bool On { get; set; } = true;

	public float MaxRenderDistance { get; set; } = 1024f;

	/// <summary>
	/// True when a linked camera is actively providing a render texture.
	/// </summary>
	public bool HasLinkedCamera => _linkedWeapon is not null && _linkedWeapon.Enabled && _linkedWeapon.RenderTexture is not null;

	private Texture _linkedTexture;
	private Texture _lastTexture;
	private CameraWeapon _linkedWeapon;
	private Material _materialCopy;
	private ModelRenderer _renderer;
	private bool _hasSignal;
	private RealTimeSince _timeSinceSignalChange;

	private static readonly float TransitionDuration = 0.4f;
	private static readonly float FadeStartFraction = 0.75f;

	protected override void OnStart()
	{
		_renderer = GetComponentInChildren<ModelRenderer>( true );
		_renderer?.SceneObject.Batchable = false;
	}

	protected override void OnUpdate()
	{
		FindLinkedTexture();

		// Distance-based fade and RT camera culling
		float distanceToCamera = Vector3.DistanceBetween( WorldPosition, Scene.Camera.WorldPosition );
		float fadeStart = MaxRenderDistance * FadeStartFraction;
		float distanceFade = 1.0f - MathX.Clamp( ( distanceToCamera - fadeStart ) / ( MaxRenderDistance - fadeStart ), 0f, 1f );
		bool tooFar = distanceFade <= 0f;

		// Enable/disable the linked RT camera based on distance
		if ( _linkedWeapon is not null )
		{
			var camera = _linkedWeapon.GetComponentInChildren<CameraComponent>( true );
			camera?.Enabled = !tooFar;
		}

		var newSignal = On && _linkedTexture is not null && !tooFar;

		if ( newSignal != _hasSignal )
		{
			_timeSinceSignalChange = 0;
			_hasSignal = newSignal;
		}

		// Keep the last known texture alive during the off-transition,
		// but only if the linked weapon still has a valid render target.
		if ( _linkedTexture is not null )
		{
			_lastTexture = _linkedTexture;
		}
		else if ( _linkedWeapon is null || !_linkedWeapon.Enabled || _linkedWeapon.RenderTexture is null )
		{
			// Weapon gone or disabled — its texture was disposed, don't use the cached copy.
			_lastTexture = null;
		}

		EnsureMaterialSetup();

		if ( _materialCopy is null || _renderer is null ) return;

		var inTransition = _timeSinceSignalChange < TransitionDuration;
		var textureToUse = _linkedTexture ?? ( inTransition ? _lastTexture : null );

		_renderer.Attributes.Set( "Color", textureToUse is not null ? textureToUse : Texture.Black );

		if ( !_hasSignal && !inTransition )
		{
			_lastTexture = null;
		}

		_renderer.Attributes.Set( "HasSignal", _hasSignal ? 1.0f : 0.0f );
		_renderer.Attributes.Set( "ScreenOn", On ? 1.0f : 0.0f );
		_renderer.Attributes.Set( "TimeSinceSignalChange", (float)_timeSinceSignalChange );
		_renderer.Attributes.Set( "DistanceFade", distanceFade );
		_renderer.Attributes.Set( "Brightness", Brightness );
	}

	protected override void OnDestroy()
	{
		_materialCopy = null;
		_linkedTexture = null;
		base.OnDestroy();
	}

	/// <summary>
	/// Resolves the linked render texture each frame by walking ManualLink components.
	/// Looks for a CameraWeapon on the linked object.
	/// </summary>
	private void FindLinkedTexture()
	{
		_linkedTexture = null;
		_linkedWeapon = null;

		foreach ( var link in GameObject.GetComponentsInChildren<ManualLink>() )
		{
			var target = link.Body?.Root;
			if ( target is null ) continue;

			if ( target.GetComponentInChildren<CameraWeapon>() is CameraWeapon weapon
				&& weapon.RenderTexture is not null )
			{
				_linkedTexture = weapon.RenderTexture;
				_linkedWeapon = weapon;
				return;
			}
		}
	}

	private static readonly string ShaderPath = "entities/sents/tv/materials/tv_crt_screen.shader";

	private void EnsureMaterialSetup()
	{
		if ( _materialCopy is not null && _renderer.IsValid() ) return;
		if ( _renderer is null ) return;

		var materials = _renderer.Model?.Materials;
		if ( materials is not { } mats ) return;

		for ( int i = 0; i < mats.Length; i++ )
		{
			if ( mats[i]?.Name?.Contains( ScreenMaterialName ) is true )
			{
				_materialCopy = Material.FromShader( ShaderPath );
				_renderer.Materials.SetOverride( i, _materialCopy );
				return;
			}
		}
	}
}
