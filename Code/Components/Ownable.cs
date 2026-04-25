using Sandbox;
using System.Text.Json.Serialization;

/// <summary>
/// Tracks which connection spawned this object
/// </summary>
public sealed class Ownable : Component, IPhysgunEvent, IToolgunEvent
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
		set => _ownerId = value?.Id ?? Guid.Empty;
	}

	public static Ownable Set( GameObject go, Connection owner )
	{
		var ownable = go.GetOrAddComponent<Ownable>();
		ownable.Owner = owner;
		return ownable;
	}

	/// <summary>
	/// When enabled, players can only physgun/toolgun objects they own.
	/// Host is always exempt. Off by default.
	/// </summary>
	[Title( "Prop Protection" )]
	[ConVar( "sb.ownership_checks", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Enforce ownership, players can only interact with their own props." )]
	public static bool OwnershipChecks { get; set; } = false;

	internal bool CallerHasAccess( Connection caller ) => HasAccess( caller, Owner );

	public static bool HasAccess( Connection caller, Connection owner )
	{
		if ( !OwnershipChecks ) return true;
		if ( caller is null ) return false;
		if ( caller.HasPermission( "admin" ) ) return true;
		if ( owner is null ) return true;
		return owner == caller;
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
}


public static class OwnableExtensions
{
	public static bool HasAccess( this GameObject go, Connection caller )
	{
		if ( go.Components.TryGet<Ownable>( out var ownable ) )
			return ownable.CallerHasAccess( caller );
		return true;
	}
}
