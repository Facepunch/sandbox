using System.Buffers;
using System.Runtime.CompilerServices;
using static Doo;

public partial class Doo
{
	internal class RunContext
	{
		public Doo Doo;
		public Component SourceComponent;
	}
}

public class DooEngine : GameObjectSystem<DooEngine>
{
	public DooEngine( Scene scene ) : base( scene )
	{

	}

	internal void Run( Component myComponent, Doo doo, Action<Doo.Configure> c )
	{
		if ( doo == null ) return;

		var ctx = new RunContext();
		ctx.Doo = doo;
		ctx.SourceComponent = myComponent;

		if ( c != null )
		{
			var config = new Doo.Configure( this, myComponent, doo );
			c( config );
		}

		_ = RunBody( ctx, doo.Body );
	}

	async Task RunBody( RunContext ctx, List<Doo.Block> b )
	{
		if ( b == null ) return;

		for ( int i = 0; i < b.Count; i++ )
		{
			await RunBlock( ctx, b[i] );
		}
	}

	async Task RunBlock( RunContext ctx, Doo.Block b )
	{
		switch ( b )
		{
			case Doo.SetBlock s:
				SetVariable( ctx, s.VariableName, Eval( s.Value ) );
				break;


			case Doo.DelayBlock d:
				await RunBlock_Delay( ctx, d );
				break;

			case Doo.ReturnBlock r:
				//	_stop = true;
				break;

			case Doo.InvokeBlock i:
				bool flowControl = RunBlock_Invoke( ctx, i );
				if ( !flowControl )
				{
					break;
				}
				break;
		}
	}

	Dictionary<string, object> _globals = new( StringComparer.OrdinalIgnoreCase );

	void SetVariable( RunContext ctx, string name, object value )
	{
		if ( string.IsNullOrWhiteSpace( name ) ) return;
		_globals[name] = value;
	}

	public void SetGlobalVariable( string name, object value )
	{
		if ( string.IsNullOrWhiteSpace( name ) ) return;
		_globals[name] = value;
	}

	public object GetVariable( string name )
	{
		if ( _globals.TryGetValue( name, out var value ) )
			return value;

		return null;
	}

	private async Task RunBlock_Delay( RunContext ctx, Doo.DelayBlock b )
	{
		double seconds = ToFloat( Eval( b.Seconds ) );
		if ( seconds < 0 ) seconds = 0;

		await Task.Delay( TimeSpan.FromSeconds( seconds ) );
	}

	private bool RunBlock_Invoke( RunContext ctx, Doo.InvokeBlock b )
	{
		var m = Doo.Helpers.FindMethod( b.Member );

		if ( m == null )
			return false;

		int argCount = m.Parameters?.Length ?? 0;

		Component targetInstance = null;

		if ( !m.IsStatic )
		{
			targetInstance = b.TargetComponent;
			if ( !targetInstance.IsValid() )
				return false;
		}

		if ( argCount == 0 )
		{
			m.Invoke( targetInstance );
			return true;
		}

		var args = ArrayPool<object>.Shared.Rent( m.Parameters.Length );

		for ( int i = 0; i < m.Parameters.Length; i++ )
		{
			args[i] = null;

			if ( b.Arguments == null || i >= b.Arguments.Count )
				continue;

			var value = Eval( b.Arguments[i] );
			args[i] = ToType( value, m.Parameters[i].ParameterType );

		}

		m.Invoke( targetInstance, args );

		ArrayPool<object>.Shared.Return( args, clearArray: true );

		return true;
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private object Eval( Doo.Expression e )
	{
		if ( e == null ) return null;

		if ( e is Doo.LiteralExpression le ) return le.LiteralValue.Value;
		if ( e is Doo.VariableExpression ve ) return GetVariable( ve.VariableName );

		return null;
	}

	static float ToFloat( object o )
	{
		if ( o == null ) return 0;
		if ( o is float f ) return f;
		if ( o is double d ) return (float)d;
		if ( o is int i ) return i;
		if ( o is long l ) return l;
		if ( o is string s && float.TryParse( s, out var result ) ) return result;
		return 0;
	}

	static object ToType( object o, Type t )
	{
		if ( t == typeof( string ) ) return o?.ToString() ?? "";
		if ( t == typeof( double ) ) return ToFloat( o );
		if ( t == typeof( float ) ) return ToFloat( o );

		return null;
	}

	static bool ToBool( object o )
	{
		if ( o == null ) return false;
		if ( o is bool b ) return b;
		if ( o is string s ) return s.ToBool();
		if ( o is float f ) return f != 0.0f;
		return o != null;
	}
}
