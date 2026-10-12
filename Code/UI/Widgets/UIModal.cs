using Sandbox.UI;
using Sandbox.UI.Construct;

namespace Sandbox;

/// <summary>
/// A small window with a title bar and a <see cref="UISheet"/> of whatever object you give it.
/// </summary>
public class UIModal : Panel
{
	readonly UISheet _sheet;
	bool _wired;
	bool _focused;
	int _framesLaidOut;

	/// <summary>
	/// Called when Enter is pressed in one of its text boxes.
	/// </summary>
	public Action OnSubmit { get; set; }

	/// <summary>
	/// Called after it closed, by any of the ways.
	/// </summary>
	public Action OnClosed { get; set; }

	public UIModal( string title, object target )
	{
		AddClass( "modal" );

		var window = Add.Panel( "window" );
		window.Add.Panel( "title-bar" ).Add.Label( title );

		_sheet = window.Add.Panel( "body" ).AddChild<UISheet>();
		_sheet.PropertyFilter = UISheet.PropertiesAndButtons;
		_sheet.Target = target;
	}

	/// <summary>
	/// Opens a window over the menu the panel is in. Only one stays open, a new one replaces it.
	/// </summary>
	public static UIModal Show( Panel source, string title, object target )
	{
		var layer = SpawnMenuHost.ModalLayer ?? source.FindPopupPanel();
		if ( layer is null ) return null;

		foreach ( var other in layer.Children.OfType<UIModal>().ToList() )
			other.Close();

		var modal = new UIModal( title, target );
		modal.Parent = layer;

		return modal;
	}

	/// <summary>
	/// A window with a text box and two buttons, for asking a name
	/// </summary>
	/// <param name="title">The title bar, can be a localization token.</param>
	/// <param name="value">What the text box starts with.</param>
	/// <param name="onConfirm">Called with the name when it is confirmed.</param>
	/// <param name="placeholder">What the empty text box says.</param>
	/// <param name="confirmText">What the confirm button says.</param>
	/// <param name="confirmIcon">The icon of the confirm button.</param>
	public static UIModal Prompt( Panel source, string title, string value, Action<string> onConfirm, string placeholder = null, string confirmText = "#ui.common.confirm", string confirmIcon = "check" )
	{
		value ??= "";
		UIModal modal = null;

		bool HasValue() => !string.IsNullOrWhiteSpace( value );

		void Confirm()
		{
			if ( !HasValue() ) return;

			onConfirm?.Invoke( value.Trim() );
			modal?.Close();
		}

		var attributes = new List<Attribute> { new PropertyAttribute(), new HideLabelAttribute() };
		if ( placeholder is not null )
			attributes.Add( new PlaceholderAttribute( placeholder ) );

		var properties = new List<SerializedProperty>
		{
			Game.TypeLibrary.CreateProperty( "Value", () => value, v => value = v, attributes.ToArray() ),
			new UIActionProperty( confirmText, confirmIcon, Confirm, 1, separator: true ) { Enabled = HasValue },
			new UIActionProperty( "#spawnmenu.common.cancel", null, () => modal?.Close(), 2, sameLine: true )
		};

		modal = Show( source, title, properties );
		if ( modal is not null )
			modal.OnSubmit = Confirm;

		return modal;
	}

	public void Submit() => OnSubmit?.Invoke();

	public void Close()
	{
		if ( !this.IsValid() ) return;

		Delete();
		OnClosed?.Invoke();
	}

	public override void Tick()
	{
		base.Tick();

		if ( _focused ) return;

		var entry = Descendants.OfType<TextEntry>().FirstOrDefault();
		if ( entry is null ) return;

		if ( !_wired )
		{
			_wired = true;
			entry.AddEventListener( "onsubmit", Submit );
			entry.AddEventListener( "oncancel", Close );
		}

		if ( entry.Box.Rect.Width <= 0f || ++_framesLaidOut < 3 ) return;

		_focused = true;
		entry.Focus();
		entry.CaretPosition = entry.TextLength;
	}

	public override void OnButtonTyped( ButtonEvent e )
	{
		if ( e.Button == "escape" )
		{
			Close();
			e.StopPropagation = true;
			return;
		}

		base.OnButtonTyped( e );
	}
}
