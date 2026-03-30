using Sandbox.UI;

public sealed partial class GameManager
{
	public static Connection FindPlayerWithName( string name, bool partial = true )
	{
		return Connection.All.FirstOrDefault( c =>
			partial
				? c.DisplayName.Contains( name, StringComparison.OrdinalIgnoreCase )
				: c.DisplayName.Equals( name, StringComparison.OrdinalIgnoreCase )
		);
	}
}
