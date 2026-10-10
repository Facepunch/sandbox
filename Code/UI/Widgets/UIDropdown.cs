using Sandbox.UI;
using Sandbox.UI.Construct;

namespace Sandbox;

public class UIDropdown : Panel
{
	/// <summary>
	/// One entry of the list. The label can be a localization token.
	/// </summary>
	public record Item( string Label, object Value );

	[Parameter]
	public List<Item> Items { get; set; } = new();

	/// <summary>
	/// An item for each value of an enum, named by its [Title].
	/// </summary>
	public static List<Item> ForEnum<T>() where T : struct, Enum
		=> DisplayInfo.ForEnumValues<T>().Select( x => new Item( x.info.Name, x.value ) ).ToList();

	/// <summary>
	/// Called with the <see cref="Item.Value"/> that was picked.
	/// </summary>
	[Parameter]
	public Action<object> ValueChanged { get; set; }

	/// <summary>
	/// Whether the labels may be localization tokens.
	/// </summary>
	[Parameter]
	public bool Tokenize { get; set; } = true;

	/// <summary>
	/// The picked value, shown in the box.
	/// </summary>
	[Parameter]
	public object Value
	{
		get => _value;
		set
		{
			_value = value;
			UpdateLabel();
		}
	}

	object _value;
	readonly Label _label;
	readonly IconPanel _chevron;
	UIDropdownList _list;
	bool _wasOpen;
	bool _closedByThisPress;

	public UIDropdown()
	{
		_label = Add.Label( "", "value" );
		_chevron = Add.Icon( "expand_more", "chevron" );
	}

	bool IsListOpen => _list.IsValid() && !_list.IsDeleting;

	public override void Tick()
	{
		base.Tick();

		var open = IsListOpen;
		_wasOpen = open;

		SetClass( "open", open );
		_chevron.Text = open ? "expand_less" : "expand_more";

		// the items can change under us (languages, dynamic lists), keep the label up to date
		UpdateLabel();
	}

	void UpdateLabel()
	{
		var current = Items.FirstOrDefault( x => Equals( x.Value, _value ) );
		_label.Tokenize = Tokenize;
		_label.Text = current?.Label ?? "";
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );

		_closedByThisPress = _wasOpen;
	}

	protected override void OnClick( MousePanelEvent e )
	{
		base.OnClick( e );

		if ( _closedByThisPress )
		{
			_closedByThisPress = false;
			return;
		}

		Open();
	}

	public void Open()
	{
		if ( IsListOpen ) return;

		_list = new UIDropdownList( this, Items, _value, Tokenize, picked =>
		{
			Value = picked.Value;
			ValueChanged?.Invoke( picked.Value );
		} );

		_list.SetClass( "big", HasClass( "big" ) );
	}
}
