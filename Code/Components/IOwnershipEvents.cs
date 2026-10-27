/// <summary>
/// Data passed to <see cref="Global.IOwnershipEvents.OnCheckAccess"/>. Set <see cref="HasAccess"/> to change the answer.
/// </summary>
public class OwnershipAccessEvent
{
	/// <summary>
	/// The object being checked. Its network root for most checks, but a child for a few (single renderers, props).
	/// </summary>
	public GameObject Target { get; init; }

	/// <summary>
	/// The connection that wants to use the object. Can be null.
	/// </summary>
	public Connection Caller { get; init; }

	/// <summary>
	/// The connection that owns the object, null if nobody does (map props, unowned objects).
	/// </summary>
	public Connection Owner { get; init; }

	/// <summary>
	/// Whether the caller may use the object. Starts as the default prop protection rule.
	/// </summary>
	public bool HasAccess { get; set; }
}

/// <summary>
/// Allows changing who may use an object. Prop protection asks this before a player can physgun, toolgun, carry,
/// wire, drive, inspect or edit an object, so one listener can add buddy lists, team sharing or locked props.
/// </summary>
public static partial class Global
{
	public interface IOwnershipEvents : ISceneEvent<IOwnershipEvents>
	{
		/// <summary>
		/// Called whenever access to an object is checked, on whichever machine asks. The host's answer is the one
		/// that counts; clients ask too, to grey out menus and refuse a grab before asking the host. Keep it cheap
		/// and give the same answer everywhere, it can run every frame.
		/// </summary>
		void OnCheckAccess( OwnershipAccessEvent e ) { }
	}
}
