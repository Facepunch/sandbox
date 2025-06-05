/// <summary>
/// A weapon, or weapons, or ammo, that can be picked up
/// </summary>
public abstract class BasePickup : Component, Component.ITriggerListener
{
	/// <summary>
	/// The pickup's collider, it is required
	/// </summary>
	[RequireComponent] public Collider Collider { get; set; }

	/// <summary>
	/// Should this pickup respawn over time, or just be destroyed on consume?
	/// </summary>
	[Property] public bool ShouldRespawn { get; set; } = true;

	/// <summary>
	/// How long should it take for this pickup to respawn?
	/// </summary>
	[Property] public float RespawnTimer { get; set; } = 5;

	/// <summary>
	/// The sound to play when picking up this item
	/// </summary>
	[Property] public SoundEvent PickupSound { get; set; }

	/// <summary>
	/// Are we allowed to pick up?
	/// </summary>
	[Sync] public bool IsPickupEnabled { get; set; } = true;

	/// <summary>
	/// Check if the player can pick up this object
	/// </summary>
	public virtual bool CanPickup( Player player, PlayerInventory inventory )
	{
		return true;
	}

	/// <summary>
	/// Give the player the effect of this pickup
	/// </summary>
	/// <returns>Should this object be consumed, eg on successful pickup</returns>
	protected virtual bool OnPickup( Player player, PlayerInventory inventory )
	{
		return true;
	}

	/// <summary>
	/// Called when a gameobject enters the trigger.
	/// </summary>
	void ITriggerListener.OnTriggerEnter( GameObject other )
	{
		if ( !Networking.IsHost ) return;
		if ( GameObject.IsDestroyed ) return;
		if ( !IsPickupEnabled ) return;

		if ( !other.Components.TryGet( out Player player ) )
			return;

		if ( !player.Components.TryGet( out PlayerInventory inventory ) )
			return;

		if ( !CanPickup( player, inventory ) )
			return;

		if ( !OnPickup( player, inventory ) )
			return;

		PlayPickupEffects( player );

		if ( ShouldRespawn )
		{
			Disable();
			Invoke( RespawnTimer, Enable );
		}
		else
		{
			DestroyGameObject();
		}
	}

	[Rpc.Broadcast]
	private void PlayPickupEffects( Player player )
	{
		if ( Application.IsDedicatedServer ) return;

		var snd = GameObject.PlaySound( PickupSound );
		if ( !snd.IsValid() )
			return;

		if ( player.IsValid() && player.IsLocalPlayer )
		{
			snd.SpacialBlend = 0;
		}
	}


	[Rpc.Broadcast]
	private void PlayRespawnEffects()
	{
		if ( Application.IsDedicatedServer ) return;

		Sound.Play( "items/item_respawn.sound", WorldPosition );
	}

	private void Enable()
	{
		IsPickupEnabled = true;
		Collider.Enabled = true;

		foreach ( var child in GameObject.Children )
		{
			child.Enabled = true;
		}

		Network.Refresh();
		PlayRespawnEffects();
	}

	private void Disable()
	{
		IsPickupEnabled = false;
		Collider.Enabled = false;

		foreach ( var child in GameObject.Children )
		{
			child.Enabled = false;
		}

		Network.Refresh();
	}
}
