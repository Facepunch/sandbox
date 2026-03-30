using Sandbox.UI;

[Icon( "👥" )]
[Title( "Users" )]
[Group( "World" )]
[Order( 1 )]
public class UsersFunction : UtilityFunction
{
	public override bool IsVisible() => Networking.IsHost;
}
