using Sandbox.Citizen;

public sealed class PlayerInventory : Component, IPlayerEvent
{
	[Property] public int MaxSlots { get; set; } = 6;

	[RequireComponent] public Player Player { get; set; }

	/// <summary>
	/// All weapons currently in the inventory, ordered by slot.
	/// </summary>
	public List<BaseCarryable> Weapons => GetComponentsInChildren<BaseCarryable>( true )
		.OrderBy( x => x.InventorySlot )
		.ToList();

	[Sync( SyncFlags.FromHost ), Change] public BaseCarryable ActiveWeapon { get; private set; }

	public void OnActiveWeaponChanged( BaseCarryable oldWeapon, BaseCarryable newWeapon )
	{
		if ( oldWeapon.IsValid() )
			oldWeapon.GameObject.Enabled = false;

		if ( newWeapon.IsValid() )
			newWeapon.GameObject.Enabled = true;
	}

	/// <summary>
	/// Returns the weapon in the given slot, or null if the slot is empty.
	/// </summary>
	public BaseCarryable GetSlot( int slot )
	{
		if ( slot < 0 || slot >= MaxSlots ) return null;
		return GetComponentsInChildren<BaseCarryable>( true )
			.FirstOrDefault( x => x.InventorySlot == slot );
	}

	/// <summary>
	/// Returns the first empty slot index, or -1 if the inventory is full.
	/// </summary>
	public int FindEmptySlot()
	{
		var occupied = GetComponentsInChildren<BaseCarryable>( true )
			.Where( x => x.InventorySlot >= 0 )
			.Select( x => x.InventorySlot )
			.ToHashSet();

		for ( int i = 0; i < MaxSlots; i++ )
		{
			if ( !occupied.Contains( i ) )
				return i;
		}

		return -1;
	}

	public void GiveDefaultWeapons()
	{
		Pickup( "weapons/physgun/physgun.prefab", false );
		Pickup( "weapons/toolgun/toolgun.prefab", false );
		Pickup( "weapons/glock/glock.prefab", false );
		Pickup( "weapons/camera/camera.prefab", 5, false );

		Player.GiveAmmo( ResourceLibrary.Get<AmmoResource>( "ammotype/9mm.ammo" ), 128, false );

		var toolgun = GetComponentInChildren<Toolgun>( true );
		toolgun?.CreateToolComponents();
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

		return Pickup( prefab, notice );
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

		return Pickup( prefab, targetSlot, notice );
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
			if ( baseCarry is BaseWeapon baseWeapon && baseWeapon.UsesAmmo )
			{
				var ammo = baseWeapon.AmmoResource;
				if ( !ammo.IsValid() )
					return false;

				if ( Player.GetAmmoCount( ammo ) >= ammo.MaxAmount )
					return false;

				Player.GiveAmmo( ammo, baseWeapon.UsesClips ? baseWeapon.ClipContents : baseWeapon.StartingAmmo, notice );

				if ( notice )
					OnClientPickup( existing, true );

				return true;
			}

			return false;
		}

		// Reject if the target slot is already occupied
		var occupant = GetSlot( targetSlot );
		if ( occupant.IsValid() )
			return false;

		var clone = prefab.Clone( new CloneConfig { Parent = GameObject, StartEnabled = false } );
		clone.NetworkSpawn( false, Network.Owner );

		var weapon = clone.Components.Get<BaseCarryable>( true );
		Assert.NotNull( weapon );

		weapon.InventorySlot = targetSlot;
		weapon.OnAdded( Player );

		IPlayerEvent.PostToGameObject( Player.GameObject, e => e.OnPickup( weapon ) );

		if ( notice )
			OnClientPickup( weapon );

		return true;
	}

	public void Take( BaseCarryable item, bool includeNotices )
	{
		var existing = Weapons.Where( x => x.GetType() == item.GetType() ).FirstOrDefault();
		if ( existing.IsValid() )
		{
			// We already have this weapon type
			if ( item is BaseWeapon baseWeapon && baseWeapon.UsesAmmo )
			{
				var ammo = baseWeapon.AmmoResource;
				if ( !ammo.IsValid() )
					return;

				if ( Player.GetAmmoCount( ammo ) >= ammo.MaxAmount )
					return;

				Player.GiveAmmo( baseWeapon.AmmoResource, baseWeapon.ClipContents, includeNotices );
				OnClientPickup( existing, true );
			}

			item.DestroyGameObject();
			return;
		}

		// Reject if the inventory is full
		var slot = FindEmptySlot();
		if ( slot < 0 )
			return;

		item.GameObject.Parent = GameObject;
		item.Network.Refresh();
		item.InventorySlot = slot;

		if ( Network.Owner is not null )
			item.Network.AssignOwnership( Network.Owner );
		else
			item.Network.DropOwnership();

		IPlayerEvent.PostToGameObject( GameObject, e => e.OnPickup( item ) );
		OnClientPickup( item );
	}

	/// <summary>
	/// Drops the given weapon from the inventory.
	/// </summary>
	public bool Drop( BaseCarryable weapon )
	{
		Assert.True( Networking.IsHost, "Must be serverside to drop" );

		if ( !weapon.IsValid() ) return false;
		if ( weapon.Owner != Player ) return false;
		if ( !weapon.ItemPrefab.IsValid() ) return false;

		var dropPosition = Player.EyeTransform.Position + Player.EyeTransform.Forward * 48f;
		var dropVelocity = Player.EyeTransform.Forward * 200f + Vector3.Up * 100f;

		// If this is the active weapon, holster first
		if ( ActiveWeapon == weapon )
		{
			SwitchWeapon( null );
		}

		// Spawn the item prefab in the world
		var pickup = weapon.ItemPrefab.Clone( new CloneConfig
		{
			Transform = new Transform( dropPosition ),
			StartEnabled = true
		} );

		pickup.NetworkSpawn();

		// Apply velocity if there's a rigidbody
		if ( pickup.GetComponent<Rigidbody>() is { } rb )
		{
			var baseVelocity = Player.Controller.Velocity;

			rb.Velocity = baseVelocity + dropVelocity;
			rb.AngularVelocity = Vector3.Random * 8.0f;
		}

		weapon.DestroyGameObject();

		// Auto-switch to best remaining weapon
		var best = GetBestWeapon();
		if ( best.IsValid() )
		{
			SwitchWeapon( best );
		}

		return true;
	}

	[Rpc.Owner]
	private void OnClientPickup( BaseCarryable weapon, bool justAmmo = false )
	{
		if ( !weapon.IsValid() ) return;

		if ( ShouldAutoswitchTo( weapon ) )
		{
			SwitchWeapon( weapon );
		}

		if ( Player.IsLocalPlayer )
			ILocalPlayerEvent.Post( e => e.OnPickup( weapon ) );
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
			var ammo = weapon.AmmoResource;
			if ( ammo.IsValid() && Player.GetAmmoCount( ammo ) < 1 )
			{
				// don't autoswitch to a weapon we've got no ammo for
				return false;
			}
		}

		return item.Value > ActiveWeapon.Value;
	}

	public BaseCarryable GetBestWeapon()
	{
		return Weapons.OrderByDescending( x => x.Value ).FirstOrDefault();
	}

	public BaseCarryable GetBestWeaponHolstered()
	{
		return Weapons.Where( x => !x.ShouldAvoid )
			.OrderByDescending( x => x.Value )
			.Where( x => x != ActiveWeapon )
			.FirstOrDefault();
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

		if ( ActiveWeapon.IsValid() )
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

	void IPlayerEvent.OnSpawned()
	{
		GiveDefaultWeapons();
	}

	void IPlayerEvent.OnDied( IPlayerEvent.DiedParams args )
	{
		if ( ActiveWeapon.IsValid() )
			ActiveWeapon.OnPlayerDeath( args );
	}

	void IPlayerEvent.OnPickup( BaseCarryable item )
	{
		if ( item is BaseWeapon weapon && weapon.IsSelfAmmo )
		{
			Player.ShowNotice( $"{weapon.AmmoResource.AmmoType} x {weapon.StartingAmmo}" );
		}
		else
		{
			Player.ShowNotice( item.DisplayName );
		}
	}

	void IPlayerEvent.OnCameraMove( ref Angles angles )
	{
		if ( ActiveWeapon.IsValid() )
		{
			ActiveWeapon.OnCameraMove( Player, ref angles );
		}
	}

	void IPlayerEvent.OnCameraPostSetup( Sandbox.CameraComponent camera )
	{
		if ( ActiveWeapon.IsValid() )
		{
			ActiveWeapon.OnCameraSetup( Player, camera );
		}
	}
}
