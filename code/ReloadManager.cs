using Sandbox;
using System.Linq;
using System.Collections.Generic;

class ReloadManager
{
	private static Dictionary<TypeDescription, IAutoload> _autoload;
	private static Dictionary<TypeDescription, IAutoload> Autoload
	{
		get
		{
			if ( _autoload == null )
				_autoload = new();
			return _autoload;
		}
	}

	public static void ReloadAutoload()
	{
		foreach ( var addon in Autoload.Keys.ToList() )
		{
			if ( Autoload[addon].ReloadOnHotload )
			{
				Autoload[addon].Dispose();
				Autoload.Remove( addon );
			}
		}

		// Init all new autoload classes
		TypeLibrary.GetTypes<IAutoload>().ToList().ForEach( x =>
		{
			if ( !x.IsAbstract && !x.IsGenericType && !Autoload.ContainsKey( x ) )
			{
				var instance = TypeLibrary.Create<IAutoload>( x.TargetType );
				instance.Initialize();
				Autoload.Add( x, instance );
			}
		} );
	}
}
