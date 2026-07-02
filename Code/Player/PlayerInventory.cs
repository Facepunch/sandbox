using Sandbox.Citizen;

public sealed class PlayerInventory : InventoryComponent, Local.IPlayerEvents
{
	// MaxSlots, ActiveItem and the active-item enable/disable + equip/holster come from the engine
	// InventoryComponent. SetDropped on equip moved to BaseCarryable.OnEquipped.

	[RequireComponent] public Player Player { get; set; }

	/// <summary>
	/// All weapons currently in the inventory, ordered by slot. Narrowing shim over engine Items.
	/// </summary>
	public IEnumerable<BaseCarryable> Weapons => Items.OfType<BaseCarryable>();

	/// <summary>
	/// The currently active weapon. Narrowing shim over the engine's <see cref="InventoryComponent.ActiveItem"/>.
	/// </summary>
	public BaseCarryable ActiveWeapon => ActiveItem as BaseCarryable;

	/// <summary>
	/// Returns the weapon in the given slot, or null if the slot is empty.
	/// </summary>
	public new BaseCarryable GetSlot( int slot ) => base.GetSlot( slot ) as BaseCarryable;

	/// <summary>
	/// Returns whether the given item could be inserted into the inventory.
	/// Checks for existing weapons that can receive ammo, and empty slots.
	/// </summary>
	public bool CanTake( BaseCarryable item )
	{
		if ( !item.IsValid() )
			return false;

		var existing = Weapons.FirstOrDefault( x => x.GetType() == item.GetType() );
		if ( existing.IsValid() )
		{
			// We already have this weapon — only allow if it can receive ammo
			if ( existing is BaseGun existingWeapon && existingWeapon.UsesAmmo )
				return existingWeapon.Ammo1 < existingWeapon.MaxReserveAmmo;

			return false;
		}

		return FindEmptySlot() >= 0;
	}

	// FindEmptySlot is inherited from the engine InventoryComponent.

	internal void GiveDefaultWeapons()
	{
		Pickup( "weapons/physgun/physgun.prefab", false );
		Pickup( "weapons/toolgun/toolgun.prefab", false );
		Pickup( "weapons/camera/camera.prefab", 8, false );
	}

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
	/// If we already own a weapon matching this prefab, try to give it ammo.
	/// Returns true if handled (caller should stop). False means no existing weapon found.
	/// </summary>
	private bool TryGiveAmmoToExisting( GameObject prefab, bool notice )
	{
		var baseCarry = prefab.Components.Get<BaseCarryable>( true );
		if ( !baseCarry.IsValid() )
			return false;

		var existing = Weapons.FirstOrDefault( x => x.GameObject.Name == prefab.Name );
		if ( !existing.IsValid() )
			return false;

		if ( existing is BaseGun existingWeapon && baseCarry is BaseGun pickupWeapon && existingWeapon.UsesAmmo )
		{
			if ( existingWeapon.Ammo1 >= existingWeapon.MaxReserveAmmo )
				return true;

			var ammoToGive = pickupWeapon.UsesClips ? pickupWeapon.ClipMaxSize : pickupWeapon.StartingAmmo;
			existingWeapon.AddReserveAmmo( ammoToGive );

			if ( notice )
				OnClientPickup( existing, true );
		}

		return true;
	}

	public bool Pickup( string prefabName, bool notice = true )
	{
		if ( !Networking.IsHost )
			return false;

		var prefab = GameObject.GetPrefab( prefabName );
		if ( prefab is null )
		{
			Log.Warning( $"Prefab not found: {prefabName}" );
			return false;
		}

		if ( TryGiveAmmoToExisting( prefab, notice ) )
			return true;

		var slot = FindEmptySlot();
		if ( slot < 0 )
			return false;

		return Pickup( prefabName, slot, notice );
	}

	public bool HasWeapon( GameObject prefab )
	{
		var baseCarry = prefab.GetComponent<BaseCarryable>( true );
		if ( !baseCarry.IsValid() )
			return false;

		return Weapons.Where( x => x.GetType() == baseCarry.GetType() )
			.FirstOrDefault()
			.IsValid();
	}

	public bool HasWeapon<T>() where T : BaseCarryable
	{
		return GetWeapon<T>().IsValid();
	}

	public T GetWeapon<T>() where T : BaseCarryable
	{
		return Weapons.OfType<T>().FirstOrDefault();
	}

	public bool Pickup( GameObject prefab, bool notice = true )
	{
		if ( TryGiveAmmoToExisting( prefab, notice ) )
			return true;

		var slot = FindEmptySlot();
		if ( slot < 0 )
			return false;

		return Pickup( prefab, slot, notice );
	}

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

		if ( !Pickup( prefab, targetSlot, notice ) )
			return false;

		return true;
	}

	public bool Pickup( GameObject prefab, int targetSlot, bool notice = true )
	{
		if ( !Networking.IsHost )
			return false;

		if ( targetSlot < 0 || targetSlot >= MaxSlots )
			return false;

		var baseCarry = prefab.Components.Get<BaseCarryable>( true );
		if ( !baseCarry.IsValid() )
			return false;

		if ( TryGiveAmmoToExisting( prefab, notice ) )
			return true;

		// Reject if the target slot is already occupied
		var occupant = GetSlot( targetSlot );
		if ( occupant.IsValid() )
			return false;

		var clone = prefab.Clone( new CloneConfig { Parent = GameObject, StartEnabled = false } );
		clone.NetworkSpawn( false, Network.Owner );

		//
		// Dropped variant components
		//
		{
			var cloneCarryable = clone.GetComponent<BaseCarryable>( true );
			cloneCarryable?.SetDropped( false );
		}

		var weapon = clone.GetComponent<BaseCarryable>( true );
		Assert.NotNull( weapon );

		weapon.InventorySlot = targetSlot;
		weapon.OnAdded( Player );

		var pickupEvent = new PlayerPickupEvent { Player = Player, Weapon = weapon, Slot = targetSlot };
		Local.IPlayerEvents.PostToGameObject( Player.GameObject, e => e.OnPickup( pickupEvent ) );
		Global.IPlayerEvents.Post( e => e.OnPlayerPickup( pickupEvent ) );

		if ( pickupEvent.Cancelled )
		{
			weapon.DestroyGameObject();
			return false;
		}

		if ( notice )
			OnClientPickup( weapon );

		return true;
	}

	/// <summary>
	/// If we already own a weapon of the same type as this live item, try to transfer its ammo.
	/// Returns true if handled (caller should stop). False means no existing weapon found.
	/// </summary>
	private bool TryGiveAmmoFromItem( BaseCarryable item, bool notice )
	{
		var existing = Weapons.FirstOrDefault( x => x.GetType() == item.GetType() );
		if ( !existing.IsValid() )
			return false;

		if ( existing is BaseGun existingWeapon && item is BaseGun pickupWeapon && existingWeapon.UsesAmmo )
		{
			if ( existingWeapon.Ammo1 >= existingWeapon.MaxReserveAmmo )
			{
				item.DestroyGameObject();
				return true;
			}

			var ammoToGive = pickupWeapon.UsesClips ? pickupWeapon.ClipMaxSize : pickupWeapon.StartingAmmo;
			existingWeapon.AddReserveAmmo( ammoToGive );

			if ( notice )
				OnClientPickup( existing, true );

			item.DestroyGameObject();
			return true;
		}

		return true;
	}

	public bool Take( BaseCarryable item, bool includeNotices )
	{
		if ( !CanTake( item ) )
			return false;

		if ( TryGiveAmmoFromItem( item, includeNotices ) )
			return true;

		var slot = FindEmptySlot();
		item.GameObject.SetParent( GameObject, false );
		item.LocalTransform = global::Transform.Zero;
		item.InventorySlot = slot;
		item.GameObject.Enabled = false;

		// Remove from undo stacks so the weapon can't be undone out of our hands
		UndoSystem.Current.Remove( item.GameObject );

		if ( Network.Owner is not null )
			item.Network.AssignOwnership( Network.Owner );
		else
			item.Network.DropOwnership();

		item.OnAdded( Player );

		var pickupEvent = new PlayerPickupEvent { Player = Player, Weapon = item, Slot = slot };
		Local.IPlayerEvents.PostToGameObject( GameObject, e => e.OnPickup( pickupEvent ) );
		Global.IPlayerEvents.Post( e => e.OnPlayerPickup( pickupEvent ) );

		if ( pickupEvent.Cancelled )
		{
			item.DestroyGameObject();
			return false;
		}

		OnClientPickup( item );
		return true;
	}

	/// <summary>
	/// Drops the given weapon from the inventory.
	/// </summary>
	public bool Drop( BaseCarryable weapon )
	{
		if ( !Networking.IsHost )
		{
			HostDrop( weapon );
			return true;
		}

		if ( !weapon.IsValid() ) return false;
		if ( weapon.Owner != Player ) return false;

		var dropEvent = new PlayerDropEvent { Player = Player, Weapon = weapon };
		Local.IPlayerEvents.PostToGameObject( Player.GameObject, e => e.OnDrop( dropEvent ) );
		Global.IPlayerEvents.Post( e => e.OnPlayerDrop( dropEvent ) );

		if ( dropEvent.Cancelled )
			return false;

		var dropPosition = Player.EyeTransform.Position + Player.EyeTransform.Forward * 48f;
		var dropVelocity = Player.EyeTransform.Forward * 200f + Vector3.Up * 100f;

		// If this is the active weapon, holster first
		if ( ActiveWeapon == weapon )
		{
			SwitchWeapon( null, true );
		}

		// The pickup is a fresh prefab clone - avoids ownership/state issues from the inventory copy.
		weapon.SpawnDroppedPickup( dropPosition, Player.Controller.Velocity + dropVelocity, Player.Network.Owner );
		weapon.DestroyGameObject();

		_ = FinishDropAsync();

		return true;
	}

	private async Task FinishDropAsync()
	{
		await Task.Yield();
		var best = GetBestWeapon();
		if ( best.IsValid() )
		{
			SwitchWeapon( best );
		}
	}

	private static SoundEvent AmmoPickupSound = ResourceLibrary.Get<SoundEvent>( "sounds/weapons/ammo_pickup.sound" );
	private static SoundEvent GunPickupSound = ResourceLibrary.Get<SoundEvent>( "sounds/weapons/gun_pickup.sound" );

	[Rpc.Owner]
	private void OnClientPickup( BaseCarryable weapon, bool justAmmo = false )
	{
		if ( !weapon.IsValid() ) return;

		if ( ShouldAutoswitchTo( weapon ) )
		{
			SwitchWeapon( weapon );
		}

		if ( Player.IsLocalPlayer )
		{
			GameObject.PlaySound( justAmmo ? AmmoPickupSound : GunPickupSound );
			Global.IPlayerEvents.Post( e => e.OnPlayerPickup( new PlayerPickupEvent { Player = Player, Weapon = weapon, Slot = weapon.InventorySlot } ) );
		}
	}

	private bool ShouldAutoswitchTo( BaseCarryable item )
	{
		Assert.True( item.IsValid(), "item invalid" );

		if ( !ActiveWeapon.IsValid() )
			return true;

		if ( !GamePreferences.AutoSwitch )
			return false;

		if ( ActiveWeapon.IsInUse() )
			return false;

		if ( item is BaseGun weapon && weapon.UsesAmmo )
		{
			if ( !weapon.HasPrimaryAmmo() && !weapon.CanReload() )
			{
				return false;
			}
		}

		return item.Value > ActiveWeapon.Value;
	}

	/// <summary>
	/// Moves the item in <paramref name="fromSlot"/> to <paramref name="toSlot"/>.
	/// If both slots are occupied the items are swapped; if <paramref name="toSlot"/> is
	/// empty the item is simply relocated.
	/// </summary>
	public new void MoveSlot( int fromSlot, int toSlot )
	{
		if ( !Networking.IsHost )
		{
			HostMoveSlot( fromSlot, toSlot );
			return;
		}

		if ( fromSlot == toSlot ) return;
		if ( fromSlot < 0 || fromSlot >= MaxSlots ) return;
		if ( toSlot < 0 || toSlot >= MaxSlots ) return;

		var fromWeapon = GetSlot( fromSlot );
		if ( !fromWeapon.IsValid() ) return;

		var moveEvent = new PlayerMoveSlotEvent { Player = Player, FromSlot = fromSlot, ToSlot = toSlot };
		Local.IPlayerEvents.PostToGameObject( Player.GameObject, e => e.OnMoveSlot( moveEvent ) );
		Global.IPlayerEvents.Post( e => e.OnPlayerMoveSlot( moveEvent ) );

		if ( moveEvent.Cancelled )
			return;

		var toWeapon = GetSlot( toSlot );

		fromWeapon.InventorySlot = toSlot;
		if ( toWeapon.IsValid() )
			toWeapon.InventorySlot = fromSlot;
	}

	[Rpc.Host]
	private void HostMoveSlot( int fromSlot, int toSlot )
	{
		MoveSlot( fromSlot, toSlot );
	}

	public BaseCarryable GetBestWeapon()
	{
		// Prefer a usable weapon by Value; only fall back to an avoid-flagged one (e.g. an empty gun) when
		// there's nothing better. The engine's GetBestItem orders purely by Value and ignores ShouldAvoid.
		var candidates = Weapons.Where( w => w.IsValid() && w.CanSwitch() ).ToList();

		return candidates.Where( w => !w.ShouldAvoid ).OrderByDescending( w => w.Value ).FirstOrDefault()
			?? candidates.OrderByDescending( w => w.Value ).FirstOrDefault();
	}

	/// <summary>
	/// Switches to the given weapon. Thin wrapper over the engine inventory's <see cref="InventoryComponent.Switch"/>
	/// (which handles host-routing and the holster veto). Switch events had no consumers and were dropped.
	/// </summary>
	public void SwitchWeapon( BaseCarryable weapon, bool allowHolster = false )
	{
		Switch( weapon, allowHolster );
	}

	// Hold type is driven by the engine BaseWeapon - set on the holder when equipped, back to "none"
	// when holstered.
	protected override void OnUpdate()
	{
		if ( ActiveWeapon.IsValid() )
		{
			ActiveWeapon.OnFrameUpdate( Player );
		}
	}

	public void OnControl()
	{
		if ( Input.Pressed( "drop" ) )
		{
			if ( ActiveWeapon.IsValid() )
				DropActiveWeapon();

			return;
		}

		// Drive the engine inventory pump - this runs the active item's per-tick control (fire, reload,
		// ADS, tool dispatch) on the owning client. Wrapped so a throwing weapon can't kill the tick.
		try
		{
			Pump();
		}
		catch ( System.Exception e )
		{
			Log.Error( e, $"Weapon control: {e.Message}" );
		}
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

	[Rpc.Host]
	private void HostDrop( BaseCarryable weapon )
	{
		Drop( weapon );
	}

	/// <summary>
	/// Removes a weapon from the inventory and destroys it without dropping it into the world.
	/// </summary>
	public void Remove( BaseCarryable weapon )
	{
		if ( !Networking.IsHost )
		{
			HostRemove( weapon );
			return;
		}
		_ = RemoveAsync( weapon );
	}

	private async Task RemoveAsync( BaseCarryable weapon )
	{
		if ( !weapon.IsValid() ) return;
		if ( weapon.Owner != Player ) return;

		var removeEvent = new PlayerRemoveWeaponEvent { Player = Player, Weapon = weapon };
		Local.IPlayerEvents.PostToGameObject( Player.GameObject, e => e.OnRemoveWeapon( removeEvent ) );
		Global.IPlayerEvents.Post( e => e.OnPlayerRemoveWeapon( removeEvent ) );

		if ( removeEvent.Cancelled )
			return;

		if ( ActiveWeapon == weapon )
			SwitchWeapon( null, true );

		weapon.DestroyGameObject();
		await Task.Yield();

		var best = GetBestWeapon();
		if ( best.IsValid() )
			SwitchWeapon( best );
	}

	[Rpc.Host]
	private void HostRemove( BaseCarryable weapon )
	{
		Remove( weapon );
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
