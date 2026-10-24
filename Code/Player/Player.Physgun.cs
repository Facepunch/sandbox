public sealed partial class Player : IPhysgunEvent
{
	[Title( "Physgun Players" ), Group( "Gameplay" ), ServerSettings.Section( "Players" ), Order( 5 )]
	[ConVar( "sandbox.allow.physgunplayers", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Players can grab other players with the physgun." )]
	public static bool AllowPhysgunPlayers { get; set; } = true;

	[Permission, Title( "Physgun Players" ), Description( "Grab other players with the physgun when it's off for everyone." )]
	public const string PhysgunPlayers = "sandbox.physgunplayers";

	public static bool CanPhysgunPlayers( Connection connection ) => Permissions.Allows( connection, AllowPhysgunPlayers, PhysgunPlayers );

	void IPhysgunEvent.OnPhysgunGrab( IPhysgunEvent.GrabEvent e )
	{
		if ( !CanPhysgunPlayers( e.Grabber ) )
			e.Cancelled = true;
	}
}
