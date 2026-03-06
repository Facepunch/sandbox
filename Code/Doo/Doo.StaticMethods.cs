public partial class Doo
{
	public static partial class Methods
	{
		[Doo.StaticMethod( "System.Quit" )]
		public static void DoSomething()
		{

		}

		[Doo.StaticMethod( "Log.Info" )]
		public static void LogInfo( string text )
		{
			Log.Info( text );
		}

		[Doo.StaticMethod( "Log.Warning" )]
		public static void LogWarning( string text )
		{
			Log.Warning( text );
		}

		[Doo.StaticMethod( "Log.Error" )]
		public static void LogError( string text )
		{
			Log.Error( text );
		}
	}
}
