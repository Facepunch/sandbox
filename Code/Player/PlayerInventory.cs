public sealed class PlayerInventory : Component, IPlayerEvent
{
	[RequireComponent] public Player Player { get; set; }

	public List<BaseCarryable> Weapons => GetComponentsInChildren<BaseCarryable>( true ).OrderBy( x => x.InventorySlot ).ThenBy( x => x.InventoryOrder ).ToList();

	[Sync] public BaseCarryable ActiveWeapon { get; private set; }

	public void GiveDefaultWeapons()
	{
		// Don't run any pickup notices when spawning in
		using var _ = Player.NoNoticeScope();

		Pickup( "weapons/camera/camera.prefab" );
		Pickup( "weapons/physgun/physgun.prefab" );
		Pickup( "weapons/toolgun/toolgun.prefab" );
		Pickup( "weapons/glock/glock.prefab" );

		Player.GiveAmmo( ResourceLibrary.Get<AmmoResource>( "ammotype/9mm.ammo" ), 200, false );

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
		var baseCarry = prefab.Components.Get<BaseCarryable>( true );
		if ( baseCarry is null )
			return false;

		return Weapons.Where( x => x.GetType() == baseCarry.GetType() ).FirstOrDefault().IsValid();
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
		if ( !Networking.IsHost )
			return false;

		var baseCarry = prefab.Components.Get<BaseCarryable>( true );
		if ( baseCarry is null )
			return false;

		var existing = Weapons.Where( x => x.GameObject.Name == prefab.Name ).FirstOrDefault();
		if ( existing.IsValid() )
		{
			// We already have this weapon type

			if ( baseCarry is BaseWeapon baseWeapon && baseWeapon.UsesAmmo )
			{
				var ammo = baseWeapon.AmmoResource;
				if ( ammo is null )
					return false;

				if ( Player.GetAmmoCount( ammo ) >= ammo.MaxAmount )
					return false;

				Player.GiveAmmo( ammo, baseWeapon.UsesClips ? baseWeapon.ClipContents : baseWeapon.StartingAmmo, notice );
				OnClientPickup( existing, true );
				return true;
			}

			return false;
		}

		var clone = prefab.Clone( new CloneConfig { Parent = GameObject, StartEnabled = false } );
		clone.NetworkSpawn( false, Network.Owner );

		var weapon = clone.Components.Get<BaseCarryable>( true );
		Assert.NotNull( weapon );

		weapon.OnAdded( Player );

		IPlayerEvent.PostToGameObject( Player.GameObject, e => e.OnPickup( weapon ) );
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
				if ( ammo is null )
					return;

				if ( Player.GetAmmoCount( ammo ) >= ammo.MaxAmount )
					return;

				Player.GiveAmmo( baseWeapon.AmmoResource, baseWeapon.ClipContents, includeNotices );
				OnClientPickup( existing, true );
			}

			item.DestroyGameObject();
			return;
		}

		item.GameObject.Parent = GameObject;
		item.Network.Refresh();

		if ( Network.Owner is not null )
		{
			item.Network.AssignOwnership( Network.Owner );
		}
		else
		{
			item.Network.DropOwnership();
		}

		IPlayerEvent.PostToGameObject( GameObject, e => e.OnPickup( item ) );
		OnClientPickup( item );
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
			if ( ammo is not null && Player.GetAmmoCount( ammo ) < 1 )
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
		return Weapons.Where( x => !x.ShouldAvoid ).OrderByDescending( x => x.Value ).Where( x => x != ActiveWeapon ).FirstOrDefault();
	}

	public void SwitchWeapon( BaseCarryable weapon )
	{
		if ( weapon == ActiveWeapon ) return;

		if ( ActiveWeapon.IsValid() )
		{
			ActiveWeapon.OnHolstered( Player );
			ActiveWeapon.GameObject.Enabled = false;
		}

		ActiveWeapon = weapon;

		if ( ActiveWeapon.IsValid() )
		{
			ActiveWeapon.OnEquipped( Player );
			ActiveWeapon.GameObject.Enabled = true;
		}
	}

	protected override void OnUpdate()
	{
		if ( ActiveWeapon.IsValid() )
		{
			ActiveWeapon.OnFrameUpdate( Player );
		}
	}

	public void OnControl()
	{
		if ( ActiveWeapon.IsValid() )
		{
			ActiveWeapon.OnPlayerUpdate( Player );
		}
	}

	void IPlayerEvent.OnSpawned()
	{
		GiveDefaultWeapons();
	}

	void IPlayerEvent.OnDied( IPlayerEvent.DiedParams args )
	{
		if ( ActiveWeapon.IsValid() )
		{
			ActiveWeapon.OnPlayerDeath( args );
		}
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
