using System;
namespace Sandbox;

public class ComponentMethodSelector : MethodSelector
{
	public override string Icon => "🧩";

	public Component TargetComponent { get; set; }

	public ComponentMethodSelector( SerializedProperty p ) : base( p )
	{

	}

	public override bool IsValidMethod( MethodDescription method )
	{
		if ( method.IsStatic ) return false;

		return true;
	}

	protected override void BuildMethods( AdvancedDropdownItem root )
	{
		if ( TargetComponent is null )
			return;

		var t = TypeLibrary.GetType( TargetComponent.GetType().FullName );

		var members = t.Members.Where( ShouldShow ).ToArray();

		foreach ( var group in members.GroupBy( x => x.DeclaringType?.Name ).OrderBy( x => x.Key ) )
		{
			var category = root.Add( group.Key );

			foreach ( var m in group.OrderBy( x => x.Name ) )
			{
				category.Add( new AdvancedDropdownItem
				{
					Title = m.Name,
					Description = m.Name,
					Value = m
				} );
			}
		}
	}

	private bool ShouldShow( MemberDescription description )
	{
		if ( description is not MethodDescription ) return false;
		if ( !description.IsPublic ) return false;
		if ( description.DeclaringType == null ) return false;

		if ( description is PropertyDescription pd )
		{
			if ( pd.PropertyType == typeof( Action ) ) return false;

		}

		return true;
	}
}

