using Sandbox.Utility;

namespace Sandbox;

/// <summary>
/// Paints the weapon buckets at the top of the screen
/// </summary>
public sealed class InventorySection : HudSection
{
	public override int Order => 10;

	public PlayerInventory Inventory { get; set; }

	public BaseSandboxWeapon Selected { get; set; }

	/// <summary>
	/// Should the menu be open right now?
	/// </summary>
	public bool Open { get; set; }

	const string Font = "Inconsolata";
	const string IndexFont = "SandboxID"; // custom, supports only digits at the moment, don't use for anything else

	const float Top = 38;
	const float TileWidth = 173;
	const float TileHeight = 101;
	const float IconAreaHeight = 78; // the icon is centred in the top part, the rest holds the name
	const float CollapsedHeight = 12.6f;
	const float Gap = 1;
	const float Border = 1;
	const float IndexInset = 2; // from the inside of the border
	const float IndexBoxSize = 15;
	const float IndexDigitSize = 7; // height of the SandboxID digit, centred in the box
	const float NameFontSize = 16;
	const float NameLetterSpacing = 0.05f; // of the font size
	const float NameBottom = 10;
	const float IconScale = 0.8f;

	const float SlideDuration = 0.1f;
	const float ExpandDuration = 0.1f;
	const float ContentFadeDuration = 0.05f;

	const float FillAlpha = 0.6f; // slot backgrounds are see-through
	const float BorderAlpha = 0.8f;
	static readonly Color FillTop = Gray( 0.224f, FillAlpha );
	static readonly Color FillBottom = Gray( 0.182f, FillAlpha );
	static readonly Color BorderColor = Gray( 0.75f, BorderAlpha );
	static readonly Color HighlightFillTop = Gray( 0.514f, FillAlpha );
	static readonly Color HighlightFillBottom = Gray( 0.344f, FillAlpha );
	static readonly Color IndexColor = Gray( 0.75f );
	static readonly Color HighlightIndexColor = Gray( 0.95f );
	static readonly Color DimIcon = Gray( 1f, 0.51f ); // icon of unselected slots
	static readonly Color DimName = Gray( 1f, 0.51f ); // name of unselected slots

	float _slide;
	float[] _expand = Array.Empty<float>();
	float[] _contentFade = Array.Empty<float>();

	int _expandedBucket = -1;
	BaseSandboxWeapon _shownSelection;

	readonly Dictionary<string, Texture> _iconCache = new();
	readonly List<List<BaseSandboxWeapon>> _columns = new();

	Rect _menuRect;

	static Color Gray( float value, float alpha = 1 ) => new( value, value, value, alpha );

	int ColumnCount => Inventory.IsValid() ? Inventory.MaxSlots : 0;

	public override void Tick()
	{
		var columns = ColumnCount;
		if ( _expand.Length != columns )
		{
			_expand = new float[columns];
			_contentFade = new float[columns];
		}

		if ( Open )
		{
			_expandedBucket = Selected.IsValid() ? Selected.Slot : -1;
			_shownSelection = Selected;
		}

		var dt = RealTime.Delta;
		_slide = Approach( _slide, Open ? 1 : 0, dt / SlideDuration );

		for ( int i = 0; i < columns; i++ )
		{
			var expanded = i == _expandedBucket;
			_expand[i] = Approach( _expand[i], expanded ? 1 : 0, dt / ExpandDuration );

			_contentFade[i] = expanded && _expand[i] >= 1 ? Approach( _contentFade[i], 1, dt / ContentFadeDuration ) : 0;
		}

		RefreshColumns();
	}

	static float Approach( float value, float target, float step ) => value < target ? MathF.Min( value + step, target ) : MathF.Max( value - step, target );

	void RefreshColumns()
	{
		var columns = ColumnCount;
		while ( _columns.Count < columns ) _columns.Add( new() );
		if ( _columns.Count > columns ) _columns.RemoveRange( columns, _columns.Count - columns );

		foreach ( var column in _columns ) column.Clear();
		if ( !Inventory.IsValid() ) return;

		foreach ( var weapon in Inventory.Weapons )
		{
			if ( weapon.Slot >= 0 && weapon.Slot < columns )
				_columns[weapon.Slot].Add( weapon );
		}
	}

	/// <summary>
	/// Collapsed columns are square, the selected one widens to 2:1 as it expands
	/// </summary>
	float ColumnWidth( int column, float scale ) => MathF.Round( MathX.Lerp( TileHeight, TileWidth, Easing.QuadraticInOut( _expand[column] ) ) * scale );

	float ColumnHeight( int column, float scale )
	{
		var items = Math.Max( _columns[column].Count, 1 );
		var child = MathX.Lerp( CollapsedHeight, TileHeight, Easing.QuadraticInOut( _expand[column] ) );
		return (TileHeight + (items - 1) * (child + Gap)) * scale;
	}

	public override void Paint( Painter painter )
	{
		_menuRect = default;
		if ( _slide <= 0 || ColumnCount == 0 ) return;

		var scale = ScaleToScreen;
		var bounds = painter.Bounds;
		var columns = ColumnCount;

		var tileHeight = MathF.Round( TileHeight * scale );
		var gap = MathF.Max( 1, MathF.Round( Gap * scale ) );

		var totalWidth = (columns - 1) * gap;
		for ( int c = 0; c < columns; c++ ) totalWidth += ColumnWidth( c, scale );
		var left = MathF.Floor( (bounds.Width - totalWidth) * 0.5f );

		var tallest = 0f;
		for ( int c = 0; c < columns; c++ ) tallest = MathF.Max( tallest, ColumnHeight( c, scale ) );

		var top = MathF.Round( Top * scale );
		var hidden = -(top + tallest + GlowRadius * scale * 2);
		var y0 = MathF.Round( MathX.Lerp( hidden, top, Easing.QuadraticInOut( _slide ) ) );

		_menuRect = new Rect( left, y0, totalWidth, tallest );

		var active = Inventory.ActiveWeapon;

		var x = left;
		for ( int c = 0; c < columns; c++ )
		{
			var tileWidth = ColumnWidth( c, scale );
			var items = _columns[c];
			var expand = Easing.QuadraticInOut( _expand[c] );
			var childHeight = MathF.Round( MathX.Lerp( CollapsedHeight, TileHeight, expand ) * scale );

			var head = items.Count > 0 ? items[0] : null;
			DrawTile( painter, new Rect( x, y0, tileWidth, tileHeight ), head, c, IsHighlighted( head, active ), _contentFade[c], scale );

			var y = y0 + tileHeight + gap;
			for ( int i = 1; i < items.Count; i++ )
			{
				var rect = new Rect( x, y, tileWidth, childHeight );
				if ( expand <= 0 )
					DrawBar( painter, rect, scale );
				else
					DrawTile( painter, rect, items[i], -1, IsHighlighted( items[i], active ), _contentFade[c], scale );

				y += childHeight + gap;
			}

			x += tileWidth + gap;
		}
	}

	bool IsHighlighted( BaseSandboxWeapon weapon, BaseSandboxWeapon active ) => weapon.IsValid() && weapon == _shownSelection;

	void DrawTile( Painter painter, Rect rect, BaseSandboxWeapon weapon, int index, bool highlighted, float contentAlpha, float scale )
	{
		var border = MathF.Max( 1, MathF.Round( Border * scale ) );
		var borderColor = highlighted ? Glowing( Tinted( Color.White ) ) : Tinted( BorderColor );

		painter.Fill = Fill.LinearGradient( Tinted( highlighted ? HighlightFillTop : FillTop ), Tinted( highlighted ? HighlightFillBottom : FillBottom ), 90 );
		painter.Rect( rect, new Vector4( border ), borderColor, borderColor, borderColor, borderColor );

		if ( index >= 0 )
		{
			var inset = border + MathF.Round( IndexInset * scale );
			var boxSize = MathF.Round( IndexBoxSize * scale );
			var box = new Rect( rect.Left + inset, rect.Top + inset, boxSize, boxSize );

			painter.Fill = highlighted ? Glowing( Tinted( HighlightIndexColor ) ) : Tinted( IndexColor );
			painter.Rect( box );

			var em = IndexDigitSize / 1.25f * FontScale;
			painter.TextStyle = new TextStyle( IndexFont, em, Tinted( highlighted ? HighlightFillTop : FillTop ).WithAlpha( 1 ) ) { Alignment = TextFlag.Center };
			painter.Text( (index + 1).ToString(), new Rect( box.Left + em * 0.125f, box.Top, box.Width, box.Height ) );
		}

		if ( !weapon.IsValid() || contentAlpha <= 0 ) return;

		using var _ = painter.Scope();
		painter.Opacity = contentAlpha;

		var glow = Glowing( Tinted( Color.White ) );
		var iconColor = highlighted ? glow : Tinted( DimIcon );
		var nameColor = highlighted ? glow : Tinted( DimName );

		if ( GetIcon( weapon ) is { } icon && icon.Width > 0 && icon.Height > 0 )
		{
			var area = new Vector2( rect.Width, IconAreaHeight * scale ) * IconScale;
			var fit = MathF.Min( area.x / icon.Width, area.y / icon.Height );
			var size = new Vector2( icon.Width, icon.Height ) * fit;
			var center = new Vector2( rect.Center.x, rect.Top + IconAreaHeight * scale * 0.5f );
			painter.Texture( icon, new Rect( center - size * 0.5f, size ), iconColor );
		}

		painter.TextStyle = new TextStyle( Font, NameFontSize * FontScale, nameColor ) { FontWeight = 700, LetterSpacing = NameFontSize * NameLetterSpacing * FontScale, Alignment = TextFlag.CenterHorizontally | TextFlag.Bottom };
		painter.Text( Localize( weapon.DisplayName ).ToUpperInvariant(), new Rect( rect.Left, rect.Top, rect.Width, rect.Height - MathF.Round( NameBottom * scale ) ) );
	}

	/// <summary>
	/// A collapsed child slot: just the frame, CollapsedHeight tall.
	/// </summary>
	void DrawBar( Painter painter, Rect rect, float scale )
	{
		var border = MathF.Max( 1, MathF.Round( Border * scale ) );
		var borderColor = Tinted( BorderColor );

		painter.Fill = Tinted( FillBottom );
		painter.Rect( rect, new Vector4( border ), borderColor, borderColor, borderColor, borderColor );
	}

	const string WeaponFallbackIcon = "ui/hud/weapon_icon_test.png";

	Texture GetIcon( BaseSandboxWeapon weapon )
	{
		if ( !string.IsNullOrEmpty( weapon.InventoryIconOverride ) ) return LoadIcon( weapon.InventoryIconOverride );
		return weapon.DisplayIcon ?? LoadIcon( WeaponFallbackIcon );
	}

	Texture LoadIcon( string path )
	{
		if ( !_iconCache.TryGetValue( path, out var texture ) )
		{
			texture = Texture.Load( path );
			_iconCache[path] = texture;
		}

		return texture;
	}

	static string Localize( string text ) => !string.IsNullOrEmpty( text ) && text.StartsWith( '#' ) ? Game.Language.GetPhrase( text[1..] ) : text ?? "";

	public override void GetCompositeRegions( List<Rect> regions )
	{
		if ( _menuRect.Width <= 0 ) return;
		regions.Add( _menuRect.Grow( MathF.Ceiling( GlowRadius * ScaleToScreen * 2 ) ) );
	}
}
