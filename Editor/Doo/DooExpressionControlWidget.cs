using Sandbox.Diagnostics;

namespace Sandbox;

/// <summary>
/// Control widget for editing Doo properties in the inspector.
/// Shows an "Edit" button that opens the Doo editor.
/// </summary>
[CustomEditor( typeof( Doo.Expression ) )]
public class DooExpressionControlWidget : ControlWidget
{
	Layout ContentLayout;

	readonly System.Type targetType;

	public DooExpressionControlWidget( SerializedProperty property ) : base( property )
	{
		Layout = Layout.Row();
		Layout.Spacing = 4;
		Layout.Margin = 0;

		if ( property.TryGetAttribute<TypeHintAttribute>( out var typeHint ) )
		{
			targetType = typeHint.HintedType;
		}

		if ( property.GetValue<Doo.Expression>() == null )
		{
			property.SetValue( new Doo.LiteralExpression() { LiteralValue = new Variant( default, targetType ) } );
		}

		if ( targetType != null && property.GetValue<Doo.LiteralExpression>() is { } expression && expression.LiteralValue.Type == null )
		{
			expression.LiteralValue = new Variant( default, targetType );
		}

		property.OnChanged += _ => BuildContent();

		ContentLayout = Layout.AddRow( 1 );

		Layout.AddStretchCell();

		BuildContent();
	}

	protected override void PaintUnder() { }
	protected override void PaintControl() { }

	public override void OnLabelContextMenu( ContextMenu menu )
	{
		var expr = SerializedProperty.GetValue<Doo.Expression>();

		// Literal submenu with value type options
		{
			var o = menu.AddOption( "Literal", "abc", () =>
			{
				SerializedProperty.SetValue( new Doo.LiteralExpression() { LiteralValue = "value" } );
				BuildContent();
			} );

			o.Checkable = true;
			o.Checked = expr is Doo.VariableExpression;
		}

		// Variable option
		{
			var o = menu.AddOption( "Variable", "abc", () =>
			{
				SerializedProperty.SetValue( new Doo.VariableExpression() { VariableName = "x" } );
				BuildContent();
			} );

			o.Checkable = true;
			o.Checked = expr is Doo.VariableExpression;
		}

	}

	void ShowExpressionTypeMenu()
	{
		var menu = new ContextMenu();

		var expr = SerializedProperty.GetValue<Doo.Expression>();

		// Literal submenu with value type options
		{
			var o = menu.AddOption( "Literal", "abc", () =>
			{
				SerializedProperty.SetValue( new Doo.LiteralExpression() { LiteralValue = "value" } );
				BuildContent();
			} );

			o.Checkable = true;
			o.Checked = expr is Doo.VariableExpression;
		}

		// Variable option
		{
			var o = menu.AddOption( "Variable", "abc", () =>
			{
				SerializedProperty.SetValue( new Doo.VariableExpression() { VariableName = "x" } );
				BuildContent();
			} );

			o.Checkable = true;
			o.Checked = expr is Doo.VariableExpression;
		}

		menu.OpenNextTo( this, WidgetAnchor.BottomEnd with { AdjustSize = true, ConstrainToScreen = true } );
	}

	Doo.Expression _old;

	void BuildContent()
	{
		if ( !SerializedProperty.TryGetAsObject( out var so ) )
			return;

		var expr = SerializedProperty.GetValue<Doo.Expression>();

		if ( _old == expr ) return;
		_old = expr;

		ContentLayout.Clear( true );
		ContentLayout.Spacing = 4;

		if ( expr is Doo.LiteralExpression )
		{
			var literalProp = so.GetProperty( nameof( Doo.LiteralExpression.LiteralValue ) );
			Assert.NotNull( literalProp );
			ContentLayout.Add( ControlWidget.Create( literalProp ) );
		}
		else if ( expr is Doo.VariableExpression )
		{
			var nameProp = so.GetProperty( nameof( Doo.VariableExpression.VariableName ) );
			Assert.NotNull( nameProp );
			ContentLayout.Add( new DooVariableControlWidget( nameProp ) );
		}
	}
}
