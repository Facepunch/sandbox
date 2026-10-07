using Sandbox.Rendering;
using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// HUD layer with glow & scanline effects
/// </summary>
public sealed class HudCanvas : Panel, IPanelDraw
{
	public Color Tint { get; set; } = Color.White;

	/// <summary>
	/// HDR color exponent for highlighted elements like vitals digits or selected hotbar slot
	/// </summary>
	public float DigitExponent { get; set; } = 3;

	/// <summary>
	/// HDR color exponent for regular text/other UI elements
	/// </summary>
	public float TextExponent { get; set; } = 1.25f;

	public float GlowStrength { get; set; } = 0.15f;

	public float GlowRadius { get; set; } = 16;

	public float ScanlineIntensity { get; set; } = 0.35f;

	/// <summary>
	/// Scanline frequency, 2 minimum
	/// </summary>
	public float ScanlinePeriod { get; set; } = 3;

	public float ScanlineThickness { get; set; } = 0.8f;

	public float ScanlineSoftness { get; set; } = 1f;

	/// <summary>
	/// Texture painters don't apply ScaleToScreen to text, so font sizes get scaled by hand
	/// </summary>
	public float FontScale { get; private set; } = 1;

	readonly List<HudSection> _sections = new();

	Texture _target;
	RenderTarget _renderTarget;
	CommandList _paintCommands;
	CameraComponent _camera;
	Material _material;

	// half resolution ping-pong targets for the glow blur
	Texture _glowA, _glowB;
	RenderTarget _glowATarget, _glowBTarget;
	Material _glowMaterial;

	const int GlowBlurTaps = 6;

	readonly List<Rect> _regions = new();

	public HudCanvas()
	{
		Style.Position = PositionMode.Absolute;
		Style.Width = Length.Percent( 100 );
		Style.Height = Length.Percent( 100 );
		Style.PointerEvents = PointerEvents.None;
	}

	public T Section<T>() where T : HudSection, new()
	{
		foreach ( var section in _sections )
		{
			if ( section is T existing ) return existing;
		}

		var added = new T { Canvas = this };
		_sections.Add( added );
		_sections.Sort( ( a, b ) => a.Order.CompareTo( b.Order ) );
		return added;
	}

	public override void Tick()
	{
		base.Tick();

		foreach ( var section in _sections )
			section.Tick();

		PaintTarget();
	}

	void PaintTarget()
	{
		var width = (int)Box.Rect.Width;
		var height = (int)Box.Rect.Height;
		if ( width <= 0 || height <= 0 ) return;

		if ( _target is null || _target.Width != width || _target.Height != height )
		{
			_target?.Dispose();
			_target = Texture.CreateRenderTarget().WithSize( width, height ).WithFormat( ImageFormat.RGBA16161616F ).Create();
			_renderTarget = RenderTarget.From( _target );
		}

		_paintCommands ??= new CommandList( GetType().Name );

		var camera = Game.ActiveScene?.Camera;
		if ( camera != _camera )
		{
			_camera?.RemoveCommandList( _paintCommands );
			camera?.AddCommandList( _paintCommands, Stage.AfterViewmodel );
			_camera = camera;
		}

		_paintCommands.Reset();
		_paintCommands.Attributes.Set( "UIGammaOutput", true );
		_paintCommands.Attributes.Set( "UIFrameGrabEncoded", true );
		_paintCommands.SetRenderTarget( _renderTarget );

		using ( var painter = Painter.Begin( _paintCommands, new Rect( 0, 0, width, height ) ) )
		{
			painter.Clear( Color.Transparent );
			FontScale = ScaleToScreen;

			foreach ( var section in _sections )
			{
				using var _ = painter.Scope();
				section.PaintTint = Tint;
				section.Paint( painter );
			}
		}

		BlurGlow( width, height );

		_paintCommands.ClearRenderTarget();
	}

	void BlurGlow( int width, int height )
	{
		var glowWidth = Math.Max( 1, width / 2 );
		var glowHeight = Math.Max( 1, height / 2 );

		if ( _glowA is null || _glowA.Width != glowWidth || _glowA.Height != glowHeight )
		{
			_glowA?.Dispose();
			_glowB?.Dispose();
			_glowA = Texture.CreateRenderTarget().WithSize( glowWidth, glowHeight ).WithFormat( ImageFormat.RGBA16161616F ).Create();
			_glowB = Texture.CreateRenderTarget().WithSize( glowWidth, glowHeight ).WithFormat( ImageFormat.RGBA16161616F ).Create();
			_glowATarget = RenderTarget.From( _glowA );
			_glowBTarget = RenderTarget.From( _glowB );
		}

		_glowMaterial ??= Material.FromShader( "shaders/hud_glow.shader" );

		var step = MathF.Max( GlowRadius * ScaleToScreen * 0.5f / GlowBlurTaps, 0.5f );

		_paintCommands.SetRenderTarget( _glowATarget );
		_paintCommands.Attributes.Set( "GlowSource", _target );
		_paintCommands.Attributes.Set( "GlowExtract", 1 );
		_paintCommands.Attributes.Set( "GlowStep", new Vector2( step / glowWidth, 0 ) );
		_paintCommands.Blit( _glowMaterial );

		_paintCommands.SetRenderTarget( _glowBTarget );
		_paintCommands.Attributes.Set( "GlowSource", _glowA );
		_paintCommands.Attributes.Set( "GlowExtract", 0 );
		_paintCommands.Attributes.Set( "GlowStep", new Vector2( 0, step / glowHeight ) );
		_paintCommands.Blit( _glowMaterial );
	}

	void IPanelDraw.Draw( CommandList commands )
	{
		if ( _target is null || _glowB is null ) return;

		_regions.Clear();
		foreach ( var section in _sections )
			section.GetCompositeRegions( _regions );
		if ( _regions.Count == 0 ) return;

		_material ??= Material.FromShader( "shaders/hud_composite.shader" );

		var scale = ScaleToScreen;
		var attributes = commands.Attributes;
		attributes.Set( "HudTexture", _target );
		attributes.Set( "HudInvSize", new Vector2( 1f / _target.Width, 1f / _target.Height ) );
		attributes.Set( "GlowTexture", _glowB );
		attributes.Set( "GlowStrength", GlowStrength );
		attributes.Set( "ScanlineIntensity", ScanlineIntensity );
		attributes.Set( "ScanlinePeriod", MathF.Max( 2, MathF.Round( ScanlinePeriod * scale ) ) );
		attributes.Set( "ScanlineThickness", ScanlineThickness );
		attributes.Set( "ScanlineSoftness", ScanlineSoftness );
		attributes.SetCombo( "D_BLENDMODE", BlendMode.Normal );

		// The glow is added onto what's behind the HUD
		attributes.GrabFrameTexture( "FrameBufferCopyTexture", Graphics.DownsampleMethod.None );

		foreach ( var region in _regions )
			commands.DrawQuad( region, _material, Color.White );
	}

	public override void OnDeleted()
	{
		if ( _paintCommands is not null ) _camera?.RemoveCommandList( _paintCommands );
		_camera = null;
		_target?.Dispose();
		_glowA?.Dispose();
		_glowB?.Dispose();
		_target = _glowA = _glowB = null;

		base.OnDeleted();
	}
}
