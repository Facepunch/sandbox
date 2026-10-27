using System.Text.Json.Serialization;

/// <summary>
/// Tracks which connection spawned this object
/// </summary>
public sealed class Ownable : Component, IPhysgunEvent, IToolgunEvent, ICarryEvent
{
	[Sync( SyncFlags.FromHost )]
	private Guid _ownerId { get; set; }

	/// <summary>
	/// I would fucking love to be able to Sync these..
	/// And it would just do this exact behaviour. Why not?
	/// </summary>
	[Property, ReadOnly, JsonIgnore]
	public Connection Owner
	{
		get => Connection.All.FirstOrDefault( c => c.Id == _ownerId );
		internal set => _ownerId = value?.Id ?? Guid.Empty;
	}

	/// <summary>
	/// Sets a gameObject as owned by a connection. If the gameObject already has an Ownable component, it'll just update the owner.
	/// </summary>
	/// <param name="go"></param>
	/// <param name="owner"></param>
	/// <returns></returns>
	public static Ownable Set( GameObject go, Connection owner )
	{
		var ownable = go.GetOrAddComponent<Ownable>();
		ownable.Owner = owner;
		return ownable;
	}

	/// <summary>
	/// When enabled, players can only physgun/toolgun/pick up objects they own.
	/// Admins are always exempt. Off by default.
	/// </summary>
	[Title( "Prop Protection" ), Group( "Gameplay" ), ServerSettings.Section( "Props" ), Order( 20 )]
	[ConVar( "sandbox.ownership.checks", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Enforce ownership, players can only interact with their own props." )]
	public static bool OwnershipChecks { get; private set; } = false;

	internal bool CallerHasAccess( Connection caller ) => CheckAccess( GameObject, caller, Owner );

	/// <summary>
	/// The default prop protection rule, before <see cref="Global.IOwnershipEvents"/> listeners get a say.
	/// </summary>
	public static bool HasAccess( Connection caller, Connection owner )
	{
		if ( !OwnershipChecks ) return true;
		if ( caller is null ) return false;
		if ( Permissions.Has( caller, Permissions.Admin ) ) return true;
		if ( owner is null ) return true;
		return owner == caller;
	}

	/// <summary>
	/// Apply the default rule, then let <see cref="Global.IOwnershipEvents"/> listeners change the answer.
	/// </summary>
	internal static bool CheckAccess( GameObject target, Connection caller, Connection owner )
	{
		var e = new OwnershipAccessEvent
		{
			Target = target,
			Caller = caller,
			Owner = owner,
			HasAccess = HasAccess( caller, owner )
		};

		var scene = target.IsValid() ? target.Scene : Game.ActiveScene;
		scene?.RunEvent<Global.IOwnershipEvents>( x => x.OnCheckAccess( e ) );

		return e.HasAccess;
	}

	void IPhysgunEvent.OnPhysgunGrab( IPhysgunEvent.GrabEvent e )
	{
		if ( !CallerHasAccess( e.Grabber ) )
			e.Cancelled = true;
	}

	void IToolgunEvent.OnToolgunSelect( IToolgunEvent.SelectEvent e )
	{
		if ( !CallerHasAccess( e.User ) )
			e.Cancelled = true;
	}

	void ICarryEvent.OnCarryGrab( PlayerCarryEvent e )
	{
		if ( !CallerHasAccess( e.Player.Network.Owner ) )
			e.Cancelled = true;
	}
}

public static class OwnableExtensions
{
	public static bool HasAccess( this GameObject go, Connection caller )
	{
		if ( go.Components.TryGet<Ownable>( out var ownable ) )
			return ownable.CallerHasAccess( caller );
		return Ownable.CheckAccess( go, caller, null );
	}
}
