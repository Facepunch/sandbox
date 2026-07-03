using Sandbox.Citizen;

public sealed class PlayerInventory : InventoryComponent, Local.IPlayerEvents
{
	// MaxSlots, ActiveItem, the active-item enable/disable + equip/holster and the add/remove/drop/
	// move-slot flows come from the engine InventoryComponent. This adds the sandbox's events, ammo
	// merging, notices and undo handling through the engine's hooks.

	[RequireComponent] public Player Player { get; set; }

	/// <summary>
	/// All weapons currently in the inventory, ordered by slot. Narrowing shim over engine Items.
	/// </summary>
	public IEnumerable<BaseSandboxWeapon> Weapons => Items.OfType<BaseSandboxWeapon>();

	/// <summary>
	/// The currently active weapon. Narrowing shim over the engine's <see cref="InventoryComponent.ActiveItem"/>.
	/// </summary>
	public BaseSandboxWeapon ActiveWeapon => ActiveItem as BaseSandboxWeapon;

	/// <summary>
	/// Returns the weapon in the given slot, or null if the slot is empty.
	/// </summary>
	public new BaseSandboxWeapon GetSlot( int slot ) => base.GetSlot( slot ) as BaseSandboxWeapon;

	/// <summary>
	/// Returns whether the given item could be inserted into the inventory.
	/// Checks for existing weapons that can receive ammo, and empty slots.
	/// </summary>
	public bool CanTake( BaseSandboxWeapon item )
	{
		if ( !item.IsValid() )
			return false;

		var existing = Weapons.FirstOrDefault( x => x.GetType() == item.GetType() );
		if ( existing.IsValid() )
		{
			// We already have this weapon — only allow if it can receive ammo
			if ( existing is BaseSandboxWeapon existingWeapon && existingWeapon.UsesAmmo )
				return existingWeapon.Ammo1 < existingWeapon.MaxReserveAmmo;

			return false;
		}

		return FindEmptySlot() >= 0;
	}

	// FindEmptySlot is inherited from the engine InventoryComponent.

	// The default weapons come from the engine's Loadout feature, configured on the player prefab -
	// PlayerLoadout calls GiveLoadout() when there's no saved hotbar to restore.

	/// <summary>
	/// Activates the named tool mode, giving and equipping the toolgun first if the player doesn't have one.
	/// </summary>
	public void SetToolMode( string name )
	{
		if ( !Networking.IsHost )
		{
			HostSetToolMode( name );
			return;
		}

		if ( !HasWeapon<Toolgun>() )
		{
			Pickup( "weapons/toolgun/toolgun.prefab", false );
		}

		var toolGun = GetWeapon<Toolgun>();
		if ( !toolGun.IsValid() ) 
			return;

		SwitchWeapon( toolGun );
		toolGun.SetToolMode( name );
	}

	[Rpc.Host]
	private void HostSetToolMode( string toolModeName )
	{
		SetToolMode( toolModeName );
	}

	/// <summary>
	/// Tops up an owned weapon's reserve from a duplicate pickup and notifies. Returns true when there
	/// is an existing weapon (the pickup is consumed either way); false when we don't own one.
	/// </summary>
	private bool TryGiveAmmo( BaseSandboxWeapon existing, BaseSandboxWeapon pickup, bool notice )
	{
		if ( !existing.IsValid() )
			return false;

		if ( existing.UsesAmmo && existing.Ammo1 < existing.MaxReserveAmmo )
		{
			GiveAmmo( existing.PrimaryAmmoType, pickup.UsesClips ? pickup.ClipMaxSize : pickup.StartingAmmo );

			if ( notice )
				OnClientPickup( existing, true );
		}

		return true;
	}

	/// <summary>
	/// If we already own a weapon matching this prefab, give it the pickup's ammo instead.
	/// Returns true if handled (caller should stop). False means no existing weapon found.
	/// </summary>
	private bool TryGiveAmmoToExisting( GameObject prefab, bool notice )
	{
		var pickup = prefab.Components.Get<BaseSandboxWeapon>( true );
		if ( !pickup.IsValid() )
			return false;

		var existing = Weapons.FirstOrDefault( x => x.GameObject.Name == prefab.Name );
		return TryGiveAmmo( existing, pickup, notice );
	}

	public bool Pickup( string prefabName, bool notice = true ) => Pickup( prefabName, -1, notice );

	public bool HasWeapon( GameObject prefab )
	{
		var baseCarry = prefab.GetComponent<BaseSandboxWeapon>( true );
		if ( !baseCarry.IsValid() )
			return false;

		return Weapons.Where( x => x.GetType() == baseCarry.GetType() )
			.FirstOrDefault()
			.IsValid();
	}

	public bool HasWeapon<T>() where T : BaseSandboxWeapon
	{
		return GetWeapon<T>().IsValid();
	}

	public T GetWeapon<T>() where T : BaseSandboxWeapon
	{
		return Weapons.OfType<T>().FirstOrDefault();
	}

	public bool Pickup( GameObject prefab, bool notice = true ) => Pickup( prefab, -1, notice );

	public bool Pickup( string prefabName, int targetSlot, bool notice = true )
	{
		if ( !Networking.IsHost )
			return false;

		var prefab = GameObject.GetPrefab( prefabName );
		if ( prefab is null )
		{
			Log.Warning( $"Prefab not found: {prefabName}" );
			return false;
		}

		return Pickup( prefab, targetSlot, notice );
	}

	public bool Pickup( GameObject prefab, int targetSlot, bool notice = true )
	{
		if ( !Networking.IsHost )
			return false;

		// Already own one of these? Give its ammo instead of a second copy.
		if ( TryGiveAmmoToExisting( prefab, notice ) )
			return true;

		// Engine pickup: clone, network spawn, parent, slot, ownership. The cancellable pickup event
		// fires from OnAdding; the engine destroys the clone if it's refused.
		if ( base.Pickup( prefab, targetSlot ) is not BaseSandboxWeapon weapon )
			return false;

		if ( notice )
			OnClientPickup( weapon );

		return true;
	}

	/// <summary>
	/// If we already own a weapon of the same type as this live item, transfer its ammo and consume
	/// it. Returns true if handled (caller should stop). False means no existing weapon found.
	/// </summary>
	private bool TryGiveAmmoFromItem( BaseSandboxWeapon item, bool notice )
	{
		var existing = Weapons.FirstOrDefault( x => x.GetType() == item.GetType() );
		if ( !TryGiveAmmo( existing, item, notice ) )
			return false;

		item.DestroyGameObject();
		return true;
	}

	public bool Take( BaseSandboxWeapon item, bool includeNotices )
	{
		if ( !CanTake( item ) )
			return false;

		if ( TryGiveAmmoFromItem( item, includeNotices ) )
			return true;

		// Remove from undo stacks so the weapon can't be undone out of our hands
		UndoSystem.Current.Remove( item.GameObject );

		// Engine add: parent, slot, ownership, disable. The cancellable pickup event fires from OnAdding.
		if ( !Add( item ) )
			return false;

		if ( includeNotices )
			OnClientPickup( item );

		return true;
	}

	/// <summary>
	/// Fires the cancellable pickup events before the engine adds an item.
	/// </summary>
	protected override bool OnAdding( Sandbox.BaseInventoryItem item, int slot )
	{
		if ( item is not BaseSandboxWeapon weapon )
			return true;

		var pickupEvent = new PlayerPickupEvent { Player = Player, Weapon = weapon, Slot = slot };
		Local.IPlayerEvents.PostToGameObject( Player.GameObject, e => e.OnPickup( pickupEvent ) );
		Global.IPlayerEvents.Post( e => e.OnPlayerPickup( pickupEvent ) );

		return !pickupEvent.Cancelled;
	}

	/// <summary>
	/// Drops the given weapon from the inventory. The engine holsters it, the weapon throws itself
	/// into the world (see <see cref="BaseSandboxWeapon.OnDrop"/>) and we switch to the best remaining
	/// weapon. The cancellable drop event fires from <see cref="OnDropping"/>.
	/// </summary>
	public void Drop( BaseSandboxWeapon weapon )
	{
		if ( weapon.IsValid() && weapon.Owner != Player )
			return;

		base.Drop( weapon );
	}

	protected override bool OnDropping( Sandbox.BaseInventoryItem item )
	{
		if ( item is not BaseSandboxWeapon weapon )
			return true;

		var dropEvent = new PlayerDropEvent { Player = Player, Weapon = weapon };
		Local.IPlayerEvents.PostToGameObject( Player.GameObject, e => e.OnDrop( dropEvent ) );
		Global.IPlayerEvents.Post( e => e.OnPlayerDrop( dropEvent ) );

		return !dropEvent.Cancelled;
	}

	private static SoundEvent AmmoPickupSound = ResourceLibrary.Get<SoundEvent>( "sounds/weapons/ammo_pickup.sound" );
	private static SoundEvent GunPickupSound = ResourceLibrary.Get<SoundEvent>( "sounds/weapons/gun_pickup.sound" );

	[Rpc.Owner]
	private void OnClientPickup( BaseSandboxWeapon weapon, bool justAmmo = false )
	{
		if ( !weapon.IsValid() ) return;

		if ( ShouldAutoswitchTo( weapon ) )
		{
			SwitchWeapon( weapon );
		}

		if ( Player.IsLocalPlayer )
		{
			GameObject.PlaySound( justAmmo ? AmmoPickupSound : GunPickupSound );
			Global.IPlayerEvents.Post( e => e.OnPlayerPickup( new PlayerPickupEvent { Player = Player, Weapon = weapon, Slot = weapon.Slot } ) );
		}
	}

	private bool ShouldAutoswitchTo( BaseSandboxWeapon item )
	{
		Assert.True( item.IsValid(), "item invalid" );

		if ( !ActiveWeapon.IsValid() )
			return true;

		if ( !GamePreferences.AutoSwitch )
			return false;

		if ( ActiveWeapon.IsInUse() )
			return false;

		if ( item is BaseSandboxWeapon weapon && weapon.UsesAmmo )
		{
			if ( !weapon.HasPrimaryAmmo() && !weapon.CanReload() )
			{
				return false;
			}
		}

		return item.Value > ActiveWeapon.Value;
	}

	// MoveSlot comes from the engine InventoryComponent - the cancellable move event fires from this
	// hook.
	protected override bool OnMovingSlot( int fromSlot, int toSlot )
	{
		var moveEvent = new PlayerMoveSlotEvent { Player = Player, FromSlot = fromSlot, ToSlot = toSlot };
		Local.IPlayerEvents.PostToGameObject( Player.GameObject, e => e.OnMoveSlot( moveEvent ) );
		Global.IPlayerEvents.Post( e => e.OnPlayerMoveSlot( moveEvent ) );

		return !moveEvent.Cancelled;
	}

	/// <summary>
	/// The weapon the inventory would auto-switch to. The engine's <see cref="InventoryComponent.GetBestItem"/>
	/// handles the Value ordering and the avoid-empty-guns fallback.
	/// </summary>
	public BaseSandboxWeapon GetBestWeapon() => GetBestItem() as BaseSandboxWeapon;

	/// <summary>
	/// Switches to the given weapon. Thin wrapper over the engine inventory's <see cref="InventoryComponent.Switch"/>
	/// (which handles host-routing and the holster veto). Switch events had no consumers and were dropped.
	/// </summary>
	public void SwitchWeapon( BaseSandboxWeapon weapon, bool allowHolster = false )
	{
		Switch( weapon, allowHolster );
	}

	public void OnControl()
	{
		if ( Input.Pressed( "drop" ) && ActiveWeapon.IsValid() )
			DropActiveWeapon();
	}

	/// <summary>
	/// Called by the owning client to drop their currently held weapon.
	/// </summary>
	[Rpc.Host]
	private void DropActiveWeapon()
	{
		if ( !ActiveWeapon.IsValid() ) return;
		Drop( ActiveWeapon );
	}

	/// <summary>
	/// Removes a weapon from the inventory and destroys it without dropping it into the world. The
	/// engine holsters it, destroys it and switches to the best remaining weapon. The cancellable
	/// remove event fires from <see cref="OnRemoving"/>.
	/// </summary>
	public void Remove( BaseSandboxWeapon weapon )
	{
		if ( weapon.IsValid() && weapon.Owner != Player )
			return;

		base.Remove( weapon );
	}

	protected override bool OnRemoving( Sandbox.BaseInventoryItem item )
	{
		if ( item is not BaseSandboxWeapon weapon )
			return true;

		var removeEvent = new PlayerRemoveWeaponEvent { Player = Player, Weapon = weapon };
		Local.IPlayerEvents.PostToGameObject( Player.GameObject, e => e.OnRemoveWeapon( removeEvent ) );
		Global.IPlayerEvents.Post( e => e.OnPlayerRemoveWeapon( removeEvent ) );

		return !removeEvent.Cancelled;
	}

	void Local.IPlayerEvents.OnDied( PlayerDiedParams args )
	{
		if ( ActiveWeapon.IsValid() )
		{
			ActiveWeapon.OnPlayerDeath( args );
		}
	}

	void Local.IPlayerEvents.OnCameraMove( ref Angles angles )
	{
		if ( !ActiveWeapon.IsValid() ) return;

		ActiveWeapon.OnCameraMove( Player, ref angles );
	}

	void Local.IPlayerEvents.OnCameraPostSetup( Sandbox.CameraComponent camera )
	{
		if ( !ActiveWeapon.IsValid() ) return;

		ActiveWeapon.OnCameraSetup( Player, camera );
	}
}
