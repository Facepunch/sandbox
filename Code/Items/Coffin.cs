/// <summary>
/// It's not the coughing you're coughin', it's the coffin they carry you off in
/// </summary>
public sealed class Coffin : Component, Component.ITriggerListener
{
	/// <summary>
	/// Sound to play when this coffin is picked up
	/// </summary>
	[Property] public SoundEvent PickupSound { get; set; }

	/// <summary>
	/// How much ammo are we holding on this coffin?
	/// </summary>
	[Sync] public Dictionary<AmmoResource, int> AmmoCounts { get; set; }

	TimeUntil timeUntilDestroy;

	protected override void OnEnabled()
	{
		timeUntilDestroy = 30;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;
		if ( timeUntilDestroy > 0 ) return;

		DestroyGameObject();
	}

	/// <summary>
	/// Called when a gameobject enters the trigger.
	/// </summary>
	void ITriggerListener.OnTriggerEnter( GameObject other )
	{
		if ( IsProxy ) return;
		if ( GameObject.IsDestroyed ) return;

		var player = other.GetComponent<Player>();
		if ( !player.IsValid() )
			return;

		PlayPickupEffects();

		if ( player.Components.TryGet<PlayerInventory>( out var inventory ) )
		{
			foreach ( var weapon in GetComponentsInChildren<BaseCarryable>( true ).ToArray() )
			{
				inventory.Take( weapon, true );
			}
		}

		foreach ( var pair in AmmoCounts )
			player.GiveAmmo( resource: pair.Key, count: pair.Value, true );

		DestroyGameObject();
	}

	/// <summary>
	/// Broadcasts a pickup effect for everyone.
	/// </summary>
	[Rpc.Broadcast]
	public void PlayPickupEffects()
	{
		if ( Application.IsDedicatedServer ) return;

		Sound.Play( PickupSound, WorldPosition );
	}
}
