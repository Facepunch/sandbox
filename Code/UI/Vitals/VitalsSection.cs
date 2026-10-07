using Sandbox.UI;
using Sandbox.Utility;

namespace Sandbox;

/// <summary>
/// Paints the health, armour and ammo HUD
/// </summary>
public sealed class VitalsSection : HudSection
{
	public struct Readout
	{
		public bool ShowVitals;
		public int Health;
		public int MaxHealth;
		public int Armour;

		public bool ShowAmmo;
		public bool UsesClips;
		public int Clip;
		public int ClipMaxSize;
		public int Reserve;

		public bool HasSecondary;
		public int Secondary;
	}

	public Readout Data { get; set; }

	/// <summary>
	/// Draws this color when ammo/health reach a critical level
	/// </summary>
	public Color WarningColor { get; set; } = new( 0.9f, 0.26f, 0f );

	public float WarningPulseAmount { get; set; } = 0.24f;

	public float WarningPulseRate { get; set; } = 0.5f;

	public float LowHealthFraction { get; set; } = 0.2f;

	public float LowAmmoFraction { get; set; } = 0.3f;

	/// <summary>
	/// The health readout flashes this color when health drops
	/// </summary>
	public Color DamageFlashColor { get; set; } = new( 1.5f, 0.4f, 0.03f );

	public float DamageFlashDuration { get; set; } = 0.15f;

	public bool Hidden { get; set; }

	const string Font = "Inconsolata";

	const float DigitWidth = 32;
	const float DigitHeight = 68;
	const float DigitFontSize = 64;

	const float TitleFontSize = 20;
	const float TitleOverlap = 10;
	const float StatGap = 44;

	const float ReserveFontSize = 24;
	const float ReserveGap = 14;
	const float ReserveBottom = 8;
	const float ReserveSpace = 62;
	const float SecondaryLineSpacing = 28;

	const int MaxClipSegments = 12;
	const float ClipBarWidth = 16;
	const float ClipBarHeight = 66;
	const float ClipBarGap = 14;
	const float ClipBarBottom = 11;
	const float ClipSegmentGap = 2;
	const float ClipSegmentSoftness = 0.5f;

	const float ShadeWidth = 600;
	const float ShadeHeight = 220;
	const float ShadeOpacity = 0.45f;

	const float HideDistance = 40;
	const float HideDuration = 0.2f;

	static readonly Color DigitColor = Color.White;
	static readonly Color TitleColor = Color.FromBytes( 236, 236, 236 );
	static readonly Color ReserveColor = Color.FromBytes( 230, 230, 230 );
	static readonly Color DepletedColor = Color.FromBytes( 68, 68, 68 );

	// randomized digit background images as a little detail
	static readonly string[] DigitBackgroundPaths = { "ui/hud/bg-digit1.png", "ui/hud/bg-digit2.png", "ui/hud/bg-digit3.png" };
	Texture[] _digitBackgrounds;

	readonly DigitCounter _health = new();
	readonly DigitCounter _armour = new();
	readonly DigitCounter _ammo = new();

	float _hideProgress;

	// 1 on the frame health drops, fading to 0 over DamageFlashDuration
	float _damageFlash;
	int? _lastHealth;

	public override void Tick()
	{
		var step = RealTime.Delta / HideDuration;
		_hideProgress = Hidden ? MathF.Min( _hideProgress + step, 1 ) : MathF.Max( _hideProgress - step, 0 );

		_damageFlash = DamageFlashDuration > 0 ? MathF.Max( _damageFlash - RealTime.Delta / DamageFlashDuration, 0 ) : 0;

		var data = Data;
		if ( !data.ShowVitals )
		{
			_lastHealth = null;
			return;
		}

		if ( data.Health < _lastHealth ) _damageFlash = 1;
		_lastHealth = data.Health;
	}

	public override void GetCompositeRegions( List<Rect> regions )
	{
		var data = Data;
		var rect = CanvasRect;
		var region = new Vector2( ShadeWidth, ShadeHeight ) * ScaleToScreen;
		if ( data.ShowVitals ) regions.Add( new Rect( rect.Left, rect.Bottom - region.y, region.x, region.y ) );
		if ( data.ShowAmmo ) regions.Add( new Rect( rect.Right - region.x, rect.Bottom - region.y, region.x, region.y ) );
	}

	public override void Paint( Painter painter )
	{
		var data = Data;
		if ( !data.ShowVitals && !data.ShowAmmo ) return;

		var hide = Easing.QuadraticInOut( _hideProgress );
		var opacity = 1 - hide;
		if ( opacity <= 0 ) return;

		_digitBackgrounds ??= DigitBackgroundPaths.Select( x => Texture.Load( x ) ).ToArray();

		var scale = ScaleToScreen;
		var bounds = painter.Bounds;
		var deadzone = Screen.Width * 0.02f;
		var bottom = MathF.Floor( bounds.Height - deadzone );

		if ( data.ShowVitals )
		{
			using var _ = painter.Scope();
			painter.Opacity = opacity;

			DrawShade( painter, new Vector2( 0, bounds.Height ), scale );

			painter.Translate( -HideDistance * scale * hide, 0 );

			var x = MathF.Floor( deadzone );
			PaintTint = DamageFlashed( SectionTint( IsLowHealth( data ) ) );
			x += DrawStat( painter, "HEALTH", _health, data.Health, 3, x, bottom, scale ) + MathF.Round( StatGap * scale );

			PaintTint = Tint;
			if ( data.Armour > 0 )
				DrawStat( painter, "ARMOR", _armour, data.Armour, 3, x, bottom, scale );
		}

		if ( data.ShowAmmo )
		{
			using var _ = painter.Scope();
			painter.Opacity = opacity;
			PaintTint = SectionTint( IsLowAmmo( data ) );

			DrawShade( painter, new Vector2( bounds.Width, bounds.Height ), scale );

			painter.Translate( HideDistance * scale * hide, 0 );

			var value = data.UsesClips ? data.Clip : data.Reserve;
			var slots = data.UsesClips ? DigitCount( data.ClipMaxSize ) : 2;
			var digitsRight = MathF.Floor( bounds.Width - deadzone - ReserveSpace * scale );
			var statX = digitsRight - slots * SlotSize( scale ).x;

			DrawStat( painter, "AMMO", _ammo, value, slots, statX, bottom, scale );

			if ( data.UsesClips || data.HasSecondary )
			{
				painter.TextStyle = new TextStyle( Font, ReserveFontSize * FontScale, Brightened( Tinted( ReserveColor ), TextExponent ) ) { FontWeight = 700, Alignment = TextFlag.LeftBottom };

				var slashWidth = painter.MeasureText( "/" ).x;
				var columnX = digitsRight + MathF.Round( ReserveGap * scale ) + slashWidth;
				var reserveBottom = bottom - MathF.Round( ReserveBottom * scale );

				if ( data.UsesClips )
				{
					painter.Text( "/", new Rect( columnX - slashWidth, 0, bounds.Width, reserveBottom ) );
					painter.Text( data.Reserve.ToString(), new Rect( columnX, 0, bounds.Width, reserveBottom ) );
				}

				if ( data.HasSecondary )
					painter.Text( data.Secondary.ToString(), new Rect( columnX, 0, bounds.Width, reserveBottom - MathF.Round( SecondaryLineSpacing * scale ) ) );
			}

			if ( data.UsesClips )
			{
				var barSize = new Vector2( MathF.Round( ClipBarWidth * scale ), MathF.Round( ClipBarHeight * scale ) );
				var barBottom = bottom - MathF.Round( ClipBarBottom * scale );
				var barRight = statX - MathF.Round( ClipBarGap * scale );
				DrawClipBar( painter, new Rect( barRight - barSize.x, barBottom - barSize.y, barSize.x, barSize.y ), data.Clip, data.ClipMaxSize, scale );
			}
		}
	}

	static Vector2 SlotSize( float scale ) => new( MathF.Ceiling( DigitWidth * scale ), MathF.Ceiling( DigitHeight * scale ) );

	float DrawStat( Painter painter, string title, DigitCounter counter, int value, int slots, float x, float bottom, float scale )
	{
		painter.TextStyle = new TextStyle( Font, TitleFontSize * FontScale, Brightened( Tinted( TitleColor ), TextExponent ) ) { FontWeight = 700 };
		var titleHeight = MathF.Ceiling( painter.MeasureText( title ).y );

		var slot = SlotSize( scale );
		var top = bottom - MathF.Ceiling( titleHeight + slot.y - TitleOverlap * scale );

		painter.Text( title, new Rect( x, top, 400 * scale, titleHeight ) );

		return DrawDigits( painter, counter, value, slots, new Vector2( x, bottom - slot.y ), slot );
	}

	float DrawDigits( Painter painter, DigitCounter counter, int value, int slots, Vector2 position, Vector2 size )
	{
		counter.Update( value, slots );

		painter.TextStyle = new TextStyle( Font, DigitFontSize * FontScale, Glowing( Tinted( DigitColor ) ) ) { FontWeight = 700, Alignment = TextFlag.Center };

		var tint = Tinted( Color.White );

		for ( int i = 0; i < counter.Length; i++ )
		{
			var slot = new Rect( position + new Vector2( size.x * i, 0 ), size );
			painter.Texture( _digitBackgrounds[counter.Variant( i )], slot, tint );

			var c = counter.Char( i );
			if ( c != ' ' ) painter.Text( c.ToString(), slot );
		}

		return size.x * counter.Length;
	}

	/// <summary>
	/// One segment per round, capped at <see cref="MaxClipSegments"/>. Past the cap each segment covers a share
	/// of the clip and fills by remaining fraction, rounded up so the last one stays lit until the clip is empty
	/// </summary>
	void DrawClipBar( Painter painter, Rect bar, int clip, int clipMaxSize, float scale )
	{
		var slots = Math.Clamp( clipMaxSize, 1, MaxClipSegments );
		clip = Math.Clamp( clip, 0, Math.Max( clipMaxSize, 1 ) );

		var filled = clipMaxSize <= MaxClipSegments
			? Math.Min( clip, slots )
			: (int)MathF.Ceiling( slots * clip / (float)clipMaxSize );

		var gap = ClipSegmentGap * scale;
		var height = (bar.Height - gap * (slots - 1)) / slots;

		Rect Segment( int i ) => new( bar.Left, bar.Bottom - (i + 1) * height - i * gap, bar.Width, height );

		var softness = ClipSegmentSoftness * scale;
		using ( painter.BeginLayer( bar.Grow( MathF.Ceiling( softness * 3 ) ), filter: new Painter.Filter { Blur = softness } ) )
		{
			for ( int i = 0; i < slots; i++ )
			{
				painter.Fill = Tinted( i < filled ? DigitColor : DepletedColor );
				painter.Rect( Segment( i ) );
			}
		}

		painter.Fill = Glowing( Tinted( DigitColor ) );
		for ( int i = 0; i < filled; i++ )
			painter.Rect( Segment( i ) );
	}

	/// <summary>
	/// Radial darkening centred on a screen corner (todo: make it optional?)
	/// </summary>
	static void DrawShade( Painter painter, Vector2 corner, float scale )
	{
		var radius = new Vector2( ShadeWidth, ShadeHeight ) * scale;

		painter.Fill = Fill.RadialGradient( Color.Black.WithAlpha( ShadeOpacity ), Color.Black.WithAlpha( 0 ) );
		painter.Rect( new Rect( corner - radius, radius * 2 ) );
	}


	Color SectionTint( bool warning )
	{
		if ( !warning ) return Tint;

		var wave = 0.5f - 0.5f * MathF.Cos( RealTime.Now * WarningPulseRate * MathF.PI * 2 );
		var brightness = 1 - Math.Clamp( WarningPulseAmount, 0, 1 ) * wave;
		return new Color( WarningColor.r * brightness, WarningColor.g * brightness, WarningColor.b * brightness, WarningColor.a );
	}

	Color DamageFlashed( Color tint )
	{
		if ( _damageFlash <= 0 ) return tint;
		return Color.Lerp( tint, DamageFlashColor, _damageFlash );
	}

	bool IsLowHealth( in Readout data ) => data.MaxHealth > 0 && data.Health <= data.MaxHealth * LowHealthFraction;
	bool IsLowAmmo( in Readout data )
	{
		if ( !data.UsesClips ) return data.Reserve <= 0;
		return data.Clip <= data.ClipMaxSize * LowAmmoFraction;
	}

	static int DigitCount( int value ) => Math.Max( value, 0 ).ToString().Length;

	/// <summary>
	/// Digit slots for one counter, each with one of the background variants picked randomly per digit
	/// </summary>
	sealed class DigitCounter
	{
		char[] _chars = Array.Empty<char>();
		int[] _variants = Array.Empty<int>();

		public int Length => _chars.Length;
		public char Char( int i ) => _chars[i];
		public int Variant( int i ) => _variants[i];

		public void Update( int value, int slots )
		{
			var text = Math.Max( value, 0 ).ToString().PadLeft( slots );

			if ( text.Length != _chars.Length )
			{
				_chars = new char[text.Length];
				_variants = new int[text.Length];
				Array.Fill( _chars, '\0' );
			}

			for ( int i = 0; i < text.Length; i++ )
			{
				if ( _chars[i] == text[i] ) continue;

				_chars[i] = text[i];
				_variants[i] = Random.Shared.Int( 0, DigitBackgroundPaths.Length - 1 );
			}
		}
	}
}
