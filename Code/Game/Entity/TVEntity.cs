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
			var rtCam = _linkedWeapon.GetComponentInChildren<CameraComponent>( true );
			if ( rtCam.IsValid() )
			{
				rtCam.Enabled = !tooFar;
			}
		}

		bool newSignal = On && _linkedTexture is not null && !tooFar;

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

		// Use the last texture during transition so the shader can blend smoothly
		bool inTransition = _timeSinceSignalChange < TransitionDuration;
		var textureToUse = _linkedTexture ?? ( inTransition ? _lastTexture : null );

		if ( textureToUse is not null )
		{
			_materialCopy.Attributes.Set( "Color", textureToUse );
			_renderer.SceneObject?.Attributes.Set( "Color", textureToUse );
		}
		else
		{
			_materialCopy.Attributes.Set( "Color", Texture.Black );
			_renderer.SceneObject?.Attributes.Set( "Color", Texture.Black );
		}

		// Clear the cached texture after the transition completes
		if ( !_hasSignal && !inTransition )
		{
			_lastTexture = null;
		}

		float signalFloat = _hasSignal ? 1.0f : 0.0f;
		float screenOn = On ? 1.0f : 0.0f;
		_materialCopy.Attributes.Set( "HasSignal", signalFloat );
		_materialCopy.Attributes.Set( "ScreenOn", screenOn );
		_materialCopy.Attributes.Set( "TimeSinceSignalChange", (float)_timeSinceSignalChange );
		_materialCopy.Attributes.Set( "DistanceFade", distanceFade );
		_materialCopy.Attributes.Set( "Brightness", Brightness );
		_renderer.SceneObject?.Attributes.Set( "HasSignal", signalFloat );
		_renderer.SceneObject?.Attributes.Set( "ScreenOn", screenOn );
		_renderer.SceneObject?.Attributes.Set( "TimeSinceSignalChange", (float)_timeSinceSignalChange );
		_renderer.SceneObject?.Attributes.Set( "DistanceFade", distanceFade );
		_renderer.SceneObject?.Attributes.Set( "Brightness", Brightness );
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

			if ( target.GetComponentInChildren<CameraWeapon>( true ) is CameraWeapon weapon
				&& weapon.Enabled
				&& weapon.RenderTexture is not null )
			{
				_linkedTexture = weapon.RenderTexture;
				_linkedWeapon = weapon;
				return;
			}
		}
	}

	private static readonly string CrtShaderPath = "entities/sents/tv/materials/tv_crt_screen.shader";

	private void EnsureMaterialSetup()
	{
		if ( _materialCopy is not null && _renderer.IsValid() ) return;

		_renderer = GetComponentInChildren<ModelRenderer>( true );
		if ( _renderer is null ) return;

		var materials = _renderer.Model?.Materials;
		if ( materials is not { } mats ) return;

		for ( int i = 0; i < mats.Length; i++ )
		{
			if ( mats[i]?.Name?.Contains( ScreenMaterialName ) == true )
			{
				_materialCopy = Material.FromShader( CrtShaderPath );
				_renderer.Materials.SetOverride( i, _materialCopy );
				return;
			}
		}
	}
}
