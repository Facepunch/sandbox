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

		[Doo.StaticMethod( "GameObject.Destroy" )]
		public static void GameObjectDestroy( GameObject go )
		{
			if ( !go.IsValid() ) return;
			go.Destroy();
		}

		[Doo.StaticMethod( "GameObject.CloneInPlace" )]
		public static GameObject GameObjectCloneInPlace( GameObject go )
		{
			if ( !go.IsValid() ) return null;
			return go.Clone( go.WorldTransform );
		}

		[Doo.StaticMethod( "GameObject.Clone" )]
		public static GameObject GameObjectClone( GameObject go, Vector3 position, Rotation angles, Vector3 scale )
		{
			return go?.Clone( position, angles, scale );
		}
	}
}
