using Sandbox.Internal;
using Sandbox.UI;

namespace Sandbox;

public class ControlSheet : Panel, IControlSheet
{
	public object Target { get; set; }

	Panel _body;

	/// <summary>
	/// Filter any properties that are added to this
	/// </summary>
	public Func<SerializedProperty, bool> PropertyFilter { get; set; }

	public void Rebuild()
	{
		IControlSheet sheet = this;
		_body?.Delete();

		_body = AddChild<Panel>();
		_body.AddClass( "body" );

		if ( Target is null ) return;

		var so = Game.TypeLibrary.GetSerializedObject( Target );
		IControlSheet.FilterSortAndAdd( sheet, so.ToList() );
	}

	int _hash;

	public override void Tick()
	{
		base.Tick();

		var hash = HashCode.Combine( Target );
		if ( hash != _hash )
		{
			_hash = hash;
			Rebuild();
		}
	}

	void IControlSheet.AddFeature( IControlSheet.Feature feature )
	{
		// Add feature tab
		Log.Warning( "TODO: TODO handle Feature Sheet" );

	}

	void IControlSheet.AddGroup( IControlSheet.Group group )
	{
		var g = _body.AddChild<ControlGroup>();

		var title = g.Header.AddChild<Label>( "title" );
		title.Text = group.Name;

		foreach ( var prop in group.Properties )
		{
			var row = g.Body.AddChild<ControlSheetRow>();
			row.Initialize( prop );
		}
	}

	bool IControlSheet.TestFilter( SerializedProperty prop )
	{
		return PropertyFilter?.Invoke( prop ) ?? true;
	}
}
