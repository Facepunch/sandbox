using Sandbox.UI;
using Sandbox.UI.Construct;

namespace Sandbox;

/// <summary>
/// A Vector2, Vector3 or Vector4 property, a number box for each axis with the axis letter in its color in front of it.
/// </summary>
public class UIVector : BaseControl
{
	static readonly string[] Axes = { "x", "y", "z", "w" };

	readonly List<NumberEntry> _entries = new();

	/// <summary>
	/// Whether we have a control for this type.
	/// </summary>
	public static bool Supports( Type type ) => AxisCount( type ) > 0;

	static int AxisCount( Type type )
	{
		if ( type == typeof( Vector2 ) ) return 2;
		if ( type == typeof( Vector3 ) ) return 3;
		if ( type == typeof( Vector4 ) ) return 4;
		return 0;
	}

	public override void Rebuild()
	{
		DeleteChildren( true );
		_entries.Clear();

		if ( Property is null ) return;

		var count = AxisCount( Property.PropertyType );

		for ( int i = 0; i < count; i++ )
		{
			var axis = Add.Panel( $"axis {Axes[i]}" );
			axis.Add.Panel( "letter" ).Add.Label( Axes[i].ToUpperInvariant() );

			var entry = axis.AddChild<NumberEntry>();
			entry.NumberFormat = "0.###";

			var index = i;
			entry.OnTextEdited = text => SetAxis( index, text );

			_entries.Add( entry );
		}

		// a Vector2 keeps the boxes as wide as the ones of a Vector3, so the rows of a sheet line up
		if ( count == 2 )
			Add.Panel( "axis filler" );
	}

	public override void Tick()
	{
		base.Tick();

		if ( Property is null || _entries.Count == 0 ) return;

		var values = Read();

		for ( int i = 0; i < _entries.Count; i++ )
		{
			var entry = _entries[i];
			if ( entry.HasFocus ) continue;

			var text = values[i].ToString( entry.NumberFormat );
			if ( entry.Text != text )
				entry.Text = text;
		}
	}

	void SetAxis( int index, string text )
	{
		var values = Read();
		values[index] = text.ToFloat( values[index] );
		Write( values );
	}

	float[] Read()
	{
		var type = Property.PropertyType;

		if ( type == typeof( Vector2 ) )
		{
			var v = Property.GetValue<Vector2>();
			return [v.x, v.y];
		}

		if ( type == typeof( Vector3 ) )
		{
			var v = Property.GetValue<Vector3>();
			return [v.x, v.y, v.z];
		}

		var w = Property.GetValue<Vector4>();
		return [w.x, w.y, w.z, w.w];
	}

	void Write( float[] values )
	{
		var type = Property.PropertyType;

		if ( type == typeof( Vector2 ) )
			Property.SetValue( new Vector2( values[0], values[1] ) );
		else if ( type == typeof( Vector3 ) )
			Property.SetValue( new Vector3( values[0], values[1], values[2] ) );
		else
			Property.SetValue( new Vector4( values[0], values[1], values[2], values[3] ) );
	}
}
