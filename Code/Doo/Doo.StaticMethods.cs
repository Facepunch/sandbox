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
		public static void GameObjectDestroy( GameObject gameObject )
		{
			if ( !gameObject.IsValid() ) return;
			gameObject.Destroy();
		}

		[Doo.StaticMethod( "GameObject.CloneInPlace" )]
		public static GameObject GameObjectCloneInPlace( [Description( "The gameobject you want to clone" )] GameObject gameObject )
		{
			if ( !gameObject.IsValid() ) return null;
			return gameObject.Clone( gameObject.WorldTransform );
		}

		[Doo.StaticMethod( "GameObject.Clone" )]
		public static GameObject GameObjectClone( [Description( "The gameobject you want to clone" )] GameObject gameObject, Vector3 position, Rotation angles, Vector3 scale )
		{
			return gameObject?.Clone( position, angles, scale );
		}
	}
}
