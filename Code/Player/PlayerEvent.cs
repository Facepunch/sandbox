public struct PlayerDiedParams
{
	public GameObject Attacker { get; set; }
}

public struct PlayerDamageParams
{
	public float Damage { get; set; }
	public GameObject Attacker { get; set; }
	public GameObject Weapon { get; set; }
	public TagSet Tags { get; set; }
	public Vector3 Position { get; set; }
	public Vector3 Origin { get; set; }
}

/// <summary>
/// Data passed to pickup events. Set <see cref="Cancelled"/> to true to prevent the pickup.
/// </summary>
public class PlayerPickupEvent
{
	public Player Player { get; init; }
	public BaseCarryable Weapon { get; init; }
	public int Slot { get; init; }
	public bool Cancelled { get; set; }
}

/// <summary>
/// Data passed to drop events. Set <see cref="Cancelled"/> to true to prevent the drop.
/// </summary>
public class PlayerDropEvent
{
	public Player Player { get; init; }
	public BaseCarryable Weapon { get; init; }
	public bool Cancelled { get; set; }
}

/// <summary>
/// Data passed to switch weapon events. Set <see cref="Cancelled"/> to true to prevent the switch.
/// </summary>
public class PlayerSwitchWeaponEvent
{
	public Player Player { get; init; }
	public BaseCarryable From { get; init; }
	public BaseCarryable To { get; init; }
	public bool Cancelled { get; set; }
}

/// <summary>
/// Data passed to remove weapon events. Set <see cref="Cancelled"/> to true to prevent the removal.
/// </summary>
public class PlayerRemoveWeaponEvent
{
	public Player Player { get; init; }
	public BaseCarryable Weapon { get; init; }
	public bool Cancelled { get; set; }
}

/// <summary>
/// Data passed to slot move events. Set <see cref="Cancelled"/> to true to prevent the move.
/// </summary>
public class PlayerMoveSlotEvent
{
	public Player Player { get; init; }
	public int FromSlot { get; init; }
	public int ToSlot { get; init; }
	public bool Cancelled { get; set; }
}

/// <summary>
/// Pre-damage event. Fired before damage is applied. Listeners can modify <see cref="Damage"/>
/// or set <see cref="Cancelled"/> to block damage entirely.
/// </summary>
public class PlayerDamageEvent
{
	public Player Player { get; init; }
	public DamageInfo DamageInfo { get; init; }
	public float Damage { get; set; }
	public bool Cancelled { get; set; }
}

/// <summary>
/// Pre-respawn event. Fired before the player is spawned. Listeners can modify
/// <see cref="SpawnLocation"/> to control where the player appears.
/// </summary>
public class PlayerRespawnEvent
{
	public PlayerData PlayerData { get; init; }
	public Transform SpawnLocation { get; set; }
}

/// <summary>
/// Fired when a player kills another player or NPC.
/// </summary>
public class PlayerKillEvent
{
	public Player Player { get; init; }
	public GameObject Victim { get; init; }
	public DamageInfo DamageInfo { get; init; }
}

/// <summary>
/// Events fired only to the Player's own GameObject hierarchy.
/// </summary>
public static partial class Local
{
	public interface IPlayerEvents : ISceneEvent<IPlayerEvents>
	{
		void OnSpawned() { }
		void OnDied( PlayerDiedParams args ) { }
		void OnDamage( PlayerDamageParams args ) { }
		void OnJump() { }
		void OnLand( float distance, Vector3 velocity ) { }
		void OnSuicide() { }
		void OnPickup( PlayerPickupEvent e ) { }
		void OnDrop( PlayerDropEvent e ) { }
		void OnSwitchWeapon( PlayerSwitchWeaponEvent e ) { }
		void OnRemoveWeapon( PlayerRemoveWeaponEvent e ) { }
		void OnMoveSlot( PlayerMoveSlotEvent e ) { }
		void OnDamaging( PlayerDamageEvent e ) { }
		void OnKill( PlayerKillEvent e ) { }
		void OnCameraMove( ref Angles angles ) { }
		void OnCameraSetup( CameraComponent camera ) { }
		void OnCameraPostSetup( CameraComponent camera ) { }
	}
}

/// <summary>
/// Events broadcasted to the entire scene for any player action.
/// </summary>
public static partial class Global
{
	public interface IPlayerEvents : ISceneEvent<IPlayerEvents>
	{
		void OnPlayerSpawned( Player player ) { }
		void OnPlayerDied( Player player, PlayerDiedParams args ) { }
		void OnPlayerDamage( Player player, PlayerDamageParams args ) { }
		void OnPlayerJumped( Player player ) { }
		void OnPlayerLanded( Player player, float distance, Vector3 velocity ) { }
		void OnPlayerSuicide( Player player ) { }
		void OnPlayerPickup( PlayerPickupEvent e ) { }
		void OnPlayerDrop( PlayerDropEvent e ) { }
		void OnPlayerSwitchWeapon( PlayerSwitchWeaponEvent e ) { }
		void OnPlayerRemoveWeapon( PlayerRemoveWeaponEvent e ) { }
		void OnPlayerMoveSlot( PlayerMoveSlotEvent e ) { }
		void OnPlayerDamaging( PlayerDamageEvent e ) { }
		void OnPlayerRespawning( PlayerRespawnEvent e ) { }
		void OnPlayerKill( PlayerKillEvent e ) { }
	}
}
