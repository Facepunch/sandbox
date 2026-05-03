using Sandbox.Citizen;

public sealed class PlayerInventory : Component, Local.IPlayerEvents
{
	[Property] public int MaxSlots { get; set; } = 6;

	[RequireComponent] public Player Player { get; set; }

	/// <summary>
	/// All weapons currently in the inventory, ordered by slot.
	/// </summary>
	public IEnumerable<BaseCarryable> Weapons => 
		GetComponentsInChildren<BaseCarryable>( true ).OrderBy( x => x.InventorySlot );

	[Sync( SyncFlags.FromHost ), Change] public BaseCarryable ActiveWeapon { get; private set; }

	public void OnActiveWeaponChanged( BaseCarryable oldWeapon, BaseCarryable newWeapon )
	{
		if ( oldWeapon.IsValid() )
			oldWeapon.GameObject.Enabled = false;

		if ( newWeapon.IsValid() )
		{
			newWeapon.GameObject.Enabled = true;
			newWeapon.SetDropped( false );
		}
	}

	/// <summary>
	/// Returns the weapon in the given slot, or null if the slot is empty.
	/// </summary>
	public BaseCarryable GetSlot( int slot )
	{
		if ( slot < 0 || slot >= MaxSlots ) return null;
		foreach ( var w in Weapons )
		{
			if ( w.InventorySlot == slot ) return w;
		}
		return null;
	}

	/// <summary>
	/// Returns the first empty slot index, or -1 if the inventory is full.
	/// </summary>
	public int FindEmptySlot()
	{
		var weapons = Weapons;
		for ( int i = 0; i < MaxSlots; i++ )
		{
			bool occupied = false;
			foreach ( var w in weapons )
			{
				if ( w.InventorySlot == i ) { occupied = true; break; }
			}
			if ( !occupied ) return i;
		}

		return -1;
	}

	public void GiveDefaultWeapons()
	{
		Pickup( "weapons/physgun/physgun.prefab", false );
		Pickup( "weapons/toolgun/toolgun.prefab", false );
		Pickup( "weapons/camera/camera.prefab", 8, false );
	}

	/// <summary>
	/// Activates the named tool mode, giving and equipping the toolgun first if the player doesn't have one.
	/// </summary>
	public void SetToolMode( string toolModeName )
	{
		if ( !Networking.IsHost )
		{
			HostSetToolMode( toolModeName );
			return;
		}

		if ( !HasWeapon<Toolgun>() )
			Pickup( "weapons/toolgun/toolgun.prefab", false );

		var toolgun = GetWeapon<Toolgun>();
		if ( !toolgun.IsValid() ) return;

		SwitchWeapon( toolgun );
		toolgun.SetToolMode( toolModeName );
	}

	[Rpc.Host]
	private void HostSetToolMode( string toolModeName )
	{
		SetToolMode( toolModeName );
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

		var existing = Weapons.Where( x => x.GameObject.Name == prefab.Name ).FirstOrDefault();
		if ( existing.IsValid() )
		{
			if ( existing is BaseWeapon existingWeapon && baseCarry is BaseWeapon pickupWeapon && existingWeapon.UsesAmmo )
			{
				if ( existingWeapon.ReserveAmmo >= existingWeapon.MaxReserveAmmo )
					return false;

				var ammoToGive = pickupWeapon.UsesClips ? pickupWeapon.ClipContents : pickupWeapon.StartingAmmo;
				existingWeapon.AddReserveAmmo( ammoToGive );

				if ( notice )
					OnClientPickup( existing, true );

				return true;
			}
		}

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

	public void Take( BaseCarryable item, bool includeNotices )
	{
		var existing = Weapons.FirstOrDefault( x => x.GetType() == item.GetType() );
		if ( existing.IsValid() )
		{
			if ( existing is BaseWeapon existingWeapon && item is BaseWeapon pickupWeapon && existingWeapon.UsesAmmo )
			{
				if ( existingWeapon.ReserveAmmo < existingWeapon.MaxReserveAmmo )
				{
					existingWeapon.AddReserveAmmo( pickupWeapon.ClipContents );
					OnClientPickup( existing, true );
				}
			}

			item.DestroyGameObject();
			return;
		}

		// Reject if the inventory is full
		var slot = FindEmptySlot();
		if ( slot < 0 )
			return;

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
			return;
		}

		OnClientPickup( item );
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

		// Weapons with a DroppedWeapon component: spawn a fresh prefab clone as server.
		// This avoids all ownership/state issues from the inventory copy.
		var droppedWeapon = weapon.GetComponent<DroppedWeapon>( true );
		if ( droppedWeapon.IsValid() )
		{
			var prefabSource = weapon.GameObject.PrefabInstanceSource;
			if ( !string.IsNullOrEmpty( prefabSource ) )
			{
				var prefab = GameObject.GetPrefab( prefabSource );
				if ( prefab.IsValid() )
				{
					var pickup = prefab.Clone( new CloneConfig
					{
						Transform = new Transform( dropPosition ),
						StartEnabled = true
					} );

					Ownable.Set( pickup, Player.Network.Owner );
					pickup.Tags.Add( "removable" );
					pickup.NetworkSpawn();

					if ( pickup.GetComponent<Rigidbody>() is { } rb )
					{
						rb.Velocity = Player.Controller.Velocity + dropVelocity;
						rb.AngularVelocity = Vector3.Random * 8.0f;
					}
				}
			}

			weapon.DestroyGameObject();
		}
		else
		{
			if ( !weapon.ItemPrefab.IsValid() )
			{
				weapon.DestroyGameObject();
				_ = FinishDropAsync();
				return true;
			}

			var pickup = weapon.ItemPrefab.Clone( new CloneConfig
			{
				Transform = new Transform( dropPosition ),
				StartEnabled = true
			} );

			Ownable.Set( pickup, Player.Network.Owner );
			pickup.Tags.Add( "removable" );
			pickup.NetworkSpawn();

			if ( pickup.GetComponent<Rigidbody>() is { } rb )
			{
				rb.Velocity = Player.Controller.Velocity + dropVelocity;
				rb.AngularVelocity = Vector3.Random * 8.0f;
			}

			weapon.DestroyGameObject();
		}

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
	private static SoundEvent GunPickupSound = ResourceLibrary.Get<SoundEvent>( "sounds/weapons/ammo_pickup.sound" );

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

		if ( item is BaseWeapon weapon && weapon.UsesAmmo )
		{
			if ( !weapon.HasAmmo() && !weapon.CanReload() )
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
	public void MoveSlot( int fromSlot, int toSlot )
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
		return Weapons.OrderByDescending( x => x.Value ).FirstOrDefault();
	}

	public void SwitchWeapon( BaseCarryable weapon, bool allowHolster = false )
	{
		if ( !Networking.IsHost )
		{
			HostSwitchWeapon( weapon, allowHolster );
			return;
		}

		if ( weapon == ActiveWeapon )
		{
			if ( allowHolster )
			{
				ActiveWeapon = null;
			}
			return;
		}

		var switchEvent = new PlayerSwitchWeaponEvent { Player = Player, From = ActiveWeapon, To = weapon };
		Local.IPlayerEvents.PostToGameObject( Player.GameObject, e => e.OnSwitchWeapon( switchEvent ) );
		Global.IPlayerEvents.Post( e => e.OnPlayerSwitchWeapon( switchEvent ) );

		if ( switchEvent.Cancelled )
			return;

		ActiveWeapon = weapon;
	}

	[Rpc.Host]
	private void HostSwitchWeapon( BaseCarryable weapon, bool allowHolster = false )
	{
		SwitchWeapon( weapon, allowHolster );
	}

	protected override void OnUpdate()
	{
		var renderer = Player?.Controller?.Renderer;

		if ( ActiveWeapon.IsValid() )
		{
			ActiveWeapon.OnFrameUpdate( Player );

			if ( renderer.IsValid() )
			{
				renderer.Set( "holdtype", (int)ActiveWeapon.HoldType );
			}
		}
		else
		{
			if ( renderer.IsValid() )
			{
				renderer.Set( "holdtype", (int)CitizenAnimationHelper.HoldTypes.None );
			}
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

		if ( ActiveWeapon.IsValid() && !ActiveWeapon.IsProxy )
			ActiveWeapon.OnPlayerUpdate( Player );
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

	// --- Event Handlers ---

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
