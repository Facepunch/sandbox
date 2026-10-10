using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// A resource property that shows all available options in a grid
/// </summary>
public class UIResourceGrid : BaseControl
{
	readonly List<Cell> _cells = new();
	string _extension;
	string _rendered;

	public override bool SupportsMultiEdit => false;

	public override void Rebuild()
	{
		DeleteChildren( true );
		_cells.Clear();

		if ( Property is null ) return;
		if ( !Property.TryGetAttribute<ResourceSelectAttribute>( out var attribute ) ) return;

		_extension = string.IsNullOrEmpty( attribute.Extension ) ? null : $".{attribute.Extension}";

		var resources = ResourceLibrary.GetAll<Resource>()
			.Where( x => HasExtension( x.ResourcePath ) )
			.OrderBy( x => x.ResourcePath, StringComparer.OrdinalIgnoreCase )
			.ToList();

		foreach ( var resource in resources )
		{
			var cell = AddChild( new Cell( resource, OnPicked ) );
			_cells.Add( cell );
		}

		SetClass( "empty", _cells.Count == 0 );
		_rendered = null;
		_fittedWidth = 0f;
		_fittedCount = -1;
		UpdateSelection();
	}

	bool HasExtension( string path )
	{
		if ( string.IsNullOrEmpty( path ) ) return false;
		if ( string.IsNullOrEmpty( _extension ) ) return true;

		return System.IO.Path.GetExtension( path ).Equals( _extension, StringComparison.OrdinalIgnoreCase );
	}

	void OnPicked( Resource resource )
	{
		Property.As.String = resource.ResourcePath;
		UpdateSelection();
	}

	public override void Tick()
	{
		base.Tick();

		if ( Property is null ) return;

		FitCells();

		if ( Property.As.String != _rendered )
			UpdateSelection();
	}

	float _fittedWidth;
	int _fittedCount = -1;

	// the grid's border and padding top and bottom, the room of a scrollbar, and the tallest it gets
	const float Frame = 2f + 2f;
	const float BarRoom = 9f;
	const float MaxHeight = 237f;

	/// <summary>
	/// Make sure all cells fit in grid space properly regardless of screen resolution
	/// </summary>
	void FitCells()
	{
		// inside the border, and then inside the 1px of padding each side (at least a pixel on screen)
		var inner = Box.RectInner.Width - 2f * MathF.Max( 1f, MathF.Round( ScaleToScreen ) );
		if ( inner <= 0f ) return;
		if ( MathF.Abs( inner - _fittedWidth ) < 0.5f && _fittedCount == _cells.Count ) return;

		_fittedWidth = inner;
		_fittedCount = _cells.Count;

		var toDesign = ScaleFromScreen;
		var rows = Math.Max( 1, (int)MathF.Ceiling( _cells.Count / 3f ) );

		var cell = CellPixels( inner );
		var scrolls = Frame + (rows * (cell * toDesign + 1f)) + (rows - 1) > MaxHeight;

		var bar = scrolls ? MathF.Round( BarRoom * ScaleToScreen ) : 0f;
		if ( scrolls )
			cell = CellPixels( inner - bar );

		// the bar is drawn over the right edge of the grid, so its room is padding on that side
		Style.PaddingRight = 1f + (scrolls ? BarRoom : 0f);

		// whole pixels for the gaps too, the last pixel of slack stays at the right
		var gap = MathF.Max( 1f, MathF.Floor( (inner - bar - 3f * cell - 1f) / 2f ) );
		Style.ColumnGap = gap * toDesign;

		foreach ( var c in _cells )
		{
			c.Style.Width = cell * toDesign;
			c.Style.Height = cell * toDesign + 1f;
		}
	}

	static float CellPixels( float room ) => MathF.Floor( (room - 3f) / 3f );

	void UpdateSelection()
	{
		_rendered = Property?.As.String;

		foreach ( var cell in _cells )
			cell.SetClass( "selected", cell.Matches( _rendered ) );
	}

	/// <summary>
	/// One pickable resource
	/// </summary>
	class Cell : Panel
	{
		readonly Resource _resource;
		readonly Action<Resource> _onPicked;
		readonly string _title;
		readonly string _description;

		public Cell( Resource resource, Action<Resource> onPicked )
		{
			_resource = resource;
			_onPicked = onPicked;

			_title = resource.ResourceName;
			if ( ResourceLibrary.Get<GameResource>( resource.ResourcePath ) is IDefinitionResource definition )
			{
				_title = definition.Title;
				_description = definition.Description;
			}

			AddClass( "resource-cell" );

			var thumb = Add.Panel( "thumb" );
			thumb.Style.SetBackgroundImage( $"thumb:{resource.ResourcePath}" );
		}

		public bool Matches( string path ) => !string.IsNullOrEmpty( path ) && path.Equals( _resource.ResourcePath, StringComparison.OrdinalIgnoreCase );

		protected override void OnClick( MousePanelEvent e )
		{
			base.OnClick( e );

			_onPicked?.Invoke( _resource );
		}

		public override bool HasTooltip => true;

		protected override Panel CreateTooltipPanel()
		{
			var description = string.IsNullOrWhiteSpace( _description ) ? "#ui.resource.no_description" : _description;

			var tip = new UIHintPanel( _title, description );
			tip.Parent = FindRootPanel();

			return tip;
		}
	}
}
