using System.Text.Json;

[AttributeUsage( AttributeTargets.Property )]
public sealed class CookieAttribute : Attribute
{
	public string Name { get; init; }
}

public interface ICookieSource
{
	string CookiePrefix { get; }
}

public static class CookieSourceExtensions
{
	private static bool IsCookieProperty( PropertyDescription property )
	{
		// TODO: maybe we want to support static properties too?

		if ( property.IsStatic ) return false;
		if ( !property.HasAttribute<CookieAttribute>() ) return false;
		if ( !property.CanWrite || !property.CanRead ) return false;

		return true;
	}

	private static string GetCookieName( PropertyDescription property, string prefix )
	{
		var name = property.GetCustomAttribute<CookieAttribute>()?.Name ?? property.Name;

		return !string.IsNullOrEmpty( prefix ) ? $"{prefix}.{name}" : name;
	}

	extension( ICookieSource source )
	{
		private IEnumerable<(string CookieName, PropertyDescription Property)> GetCookieProperties()
		{
			var typeDesc = TypeLibrary.GetType( source.GetType() );
			if ( typeDesc is null ) return [];

			var prefix = source.CookiePrefix;

			return typeDesc.Properties.Where( IsCookieProperty )
				.Select( x => (GetCookieName( x, prefix ), x) );
		}

		public void SaveCookies()
		{
			foreach ( var (cookieName, property) in source.GetCookieProperties() )
			{
				try
				{
					var cookieValue = property.GetValue( source );

					Game.Cookies.SetString( cookieName, JsonSerializer.Serialize( cookieValue, property.PropertyType ) );
				}
				catch ( Exception ex )
				{
					Log.Warning( ex, $"Exception while saving cookie \"{cookieName}\"." );
				}
			}
		}

		public void LoadCookies()
		{
			foreach ( var (cookieName, property) in source.GetCookieProperties() )
			{
				if ( !Game.Cookies.TryGetString( cookieName, out var jsonString ) ) continue;

				try
				{
					var cookieValue = JsonSerializer.Deserialize( jsonString, property.PropertyType );

					property.SetValue( source, cookieValue );
				}
				catch ( Exception ex )
				{
					Log.Warning( ex, $"Exception while loading cookie \"{cookieName}\"." );
				}
			}
		}
	}
}
