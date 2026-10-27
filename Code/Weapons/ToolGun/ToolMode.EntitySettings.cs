using System.Text.Json.Nodes;

public abstract partial class ToolMode
{
	/// <summary>
	/// This tool's <see cref="PropertyAttribute"/> settings that share a name and type with <typeparamref name="T"/>'s <see cref="ClientEditableAttribute"/> properties,
	/// as JSON for a spawn RPC, since only the owner has their current values. See <see cref="ApplyEntitySettings"/>.
	/// </summary>
	protected string WriteEntitySettings<T>() where T : Component
	{
		var tool = TypeLibrary.GetType( GetType() );
		var json = new JsonObject();

		foreach ( var property in GetEntitySettings( typeof( T ) ) )
		{
			var setting = tool?.GetProperty( property.Name );
			if ( setting is null || !setting.HasAttribute<PropertyAttribute>() || setting.PropertyType != property.PropertyType ) continue;

			json[property.Name] = Json.ToNode( setting.GetValue( this ), property.PropertyType );
		}

		return json.ToJsonString();
	}

	/// <summary>
	/// Host side of <see cref="WriteEntitySettings"/>: sets the settings on a newly placed entity, only ever its
	/// <see cref="ClientEditableAttribute"/> properties, the same ones its context menu can change.
	/// </summary>
	protected static void ApplyEntitySettings( Component entity, string settings )
	{
		if ( !entity.IsValid() || string.IsNullOrEmpty( settings ) ) return;

		try
		{
			if ( JsonNode.Parse( settings ) is not JsonObject json ) return;

			foreach ( var property in GetEntitySettings( entity.GetType() ) )
			{
				if ( json.TryGetPropertyValue( property.Name, out var value ) )
					property.SetValue( entity, Json.FromNode( value, property.PropertyType ) );
			}
		}
		catch ( Exception ex )
		{
			Log.Warning( ex, $"Couldn't apply tool settings to {entity}." );
		}
	}

	static IEnumerable<PropertyDescription> GetEntitySettings( Type type )
	{
		return TypeLibrary.GetType( type )?.Properties.Where( x => x.HasAttribute<ClientEditableAttribute>() && x.CanWrite ) ?? [];
	}
}
