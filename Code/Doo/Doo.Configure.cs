
public partial class Doo
{
	public readonly struct Configure
	{
		private readonly DooEngine _engine;
		private readonly Component _component;
		private readonly Doo _doo;

		public Configure( DooEngine dooEngine, Component myComponent, Doo doo )
		{
			this._engine = dooEngine;
			this._component = myComponent;
			this._doo = doo;
		}

		public void SetArgument( string name, object value )
		{
			_engine.SetGlobalVariable( name, value );
		}
	}
}
