public partial class Doo
{
	public static partial class Helpers
	{
		public static MethodDescription FindMethod( string methodPath )
		{
			var lastDot = methodPath?.LastIndexOf( '.' ) ?? -1;

			// not found
			if ( lastDot < 0 )
				return default;

			var typeName = methodPath.Substring( 0, lastDot );
			var methodName = methodPath.Substring( lastDot + 1 );

			var t = TypeLibrary.GetType( typeName );
			return t?.Methods.FirstOrDefault( x => x.Name == methodName );
		}

	}
}
