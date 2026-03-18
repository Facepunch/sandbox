
public partial class Doo
{
	public readonly struct Configure
	{
		private readonly RunContext _context;

		internal Configure( RunContext context )
		{
			this._context = context;
		}

		public void SetArgument( string name, object value )
		{
			_context.Engine.SetGlobalVariable( name, value );
		}
	}
}
