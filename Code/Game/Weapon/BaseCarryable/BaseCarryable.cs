using Sandbox.Rendering;

public partial class BaseCarryable : Sandbox.BaseWeapon, IKillIcon
{
	// DisplayName, DisplayIcon, Value, Slot, ViewModel and WorldModel are all inherited from the
	// engine BaseWeapon / BaseInventoryItem. (DisplayIcon also satisfies IKillIcon.)

	/// <summary>
	/// The prefab to spawn in the world when this item is dropped from the inventory.
	/// </summary>
	[Property, Feature( "Inventory" )] public GameObject ItemPrefab { get; set; }

	// MuzzleGameObject is inherited from the engine BaseWeapon.

	/// <summary>
	/// Used for overriding the display icon
	/// </summary>
	public virtual string InventoryIconOverride => null;

	/// <summary>
	/// Whether this weapon should be avoided when determining an item to swap to
	/// </summary>
	public virtual bool ShouldAvoid => false;

	/// <summary>
	/// If true the game should hide the hud when holding this weapon. Useful for cameras, or scopes.
	/// </summary>
	public virtual bool WantsHideHud => false;

	/// <summary>
	/// Gets a reference to the weapon model for this weapon - if there's a viewmodel, pick the viewmodel, if not, world model.
	/// </summary>
	public WeaponModel WeaponModel
	{
		get
		{
			var go = ViewModel;

			if ( Scene.Camera.IsValid() && Scene.Camera.RenderExcludeTags.Contains( "firstperson" ) ) go = default;

			if ( !go.IsValid() ) go = WorldModel;
			if ( !go.IsValid() ) go = GameObject;

			var wm = go.GetComponentInChildren<WeaponModel>();
			if ( wm.IsValid() )
				return wm;

			// Standalone weapons may have a WorldModel in their hierarchy without the stored reference
			return GameObject.GetComponentInChildren<WeaponModel>();
		}
	}

	/// <summary>
	/// The owner of this carriable
	/// </summary>
	// Hides the engine BaseWeapon.Owner (a PlayerController) with the game's Player-based owner.
	// Engine-internal code keeps using its own PlayerController owner; game code sees the Player.
	public new Player Owner
	{
		get
		{
			return GetComponentInParent<Player>( true );
		}
	}

	public bool HasOwner => Owner.IsValid();

	/// <summary>
	/// When true, seated aim uses the scene camera direction instead of the weapon's muzzle direction.
	/// Override in weapons that support player-directed aim (e.g. RPG tracked mode, Physgun aim mode).
	/// </summary>
	public virtual bool IsTargetedAim => false;

	/// <summary>
	/// Aim when this weapon isn't held by a player. Controlled from a seat with targeted aim fires
	/// where the camera looks; otherwise it fires from the muzzle. The held (player) cases - including
	/// third-person camera aim - are handled by the engine <see cref="Sandbox.BaseWeapon.AimRay"/>.
	/// </summary>
	protected override Ray UnheldAimRay
	{
		get
		{
			var seated = ClientInput.Current;
			if ( seated.IsValid() && IsTargetedAim && Scene.Camera.IsValid() )
				return Scene.Camera.Transform.World.ForwardRay;

			var muzzle = GetMuzzleTransform();
			return new Ray( muzzle.Position, muzzle.Rotation.Forward );
		}
	}

	/// <summary>
	/// The root GameObject to ignore when tracing from AimRay.
	/// </summary>
	public GameObject AimIgnoreRoot => HasOwner ? Owner.GameObject : GameObject;

	/// <summary>
	/// Who gets credit for this weapon's damage (the engine's <see cref="Sandbox.BaseWeapon.Attacker"/>).
	/// The owning player if held, the seated player if controlled from a contraption seat, otherwise
	/// the nearest kill source or the weapon itself.
	/// </summary>
	protected override GameObject Attacker
	{
		get
		{
			if ( HasOwner ) return Owner.GameObject;

			var seatedPlayer = ClientInput.Current;
			if ( seatedPlayer.IsValid() ) return seatedPlayer.GameObject;

			var killSource = GetComponentInParent<IKillSource>( true );
			if ( killSource is Component c ) return c.GameObject;

			return GameObject;
		}
	}

	/// <summary>
	/// Adds the weapon model's muzzle point on top of the engine's explicit-muzzle / self resolution
	/// (<see cref="Sandbox.BaseWeapon.GetMuzzleTransform"/>).
	/// </summary>
	public override Transform GetMuzzleTransform()
	{
		var modelMuzzle = WeaponModel?.MuzzleGameObject;
		if ( modelMuzzle.IsValid() )
			return modelMuzzle.WorldTransform;

		return base.GetMuzzleTransform();
	}

	/// <summary>
	/// The inventory slot this item is assigned to, or -1 if unassigned. Back-compat shim over the
	/// engine inventory's <see cref="BaseInventoryItem.Slot"/>.
	/// </summary>
	public int InventorySlot { get => Slot; set => Slot = value; }

	/// <summary>
	/// This is shite
	/// </summary>
	[Sync( SyncFlags.FromHost ), Change( nameof( OnItemVisibility ) )]
	public bool IsItem { get; set; } = true;

	private void OnItemVisibility( bool oldVal, bool newVal )
	{
		if ( DroppedGameObject.IsValid() )
			DroppedGameObject.Enabled = newVal;
	}

	/// <summary>
	/// Can we switch to this?
	/// </summary>
	/// <returns></returns>
	public virtual bool CanSwitch()
	{
		return true;
	}

	/// <summary>
	/// Bridges the game's <see cref="CanSwitch"/> into the engine inventory's switch gate.
	/// </summary>
	protected override bool OnCanSwitchTo() => CanSwitch();

	/// <summary>
	/// The engine creates the view/world models on equip and destroys them on holster. We additionally
	/// disable the dropped/physics components while held.
	/// </summary>
	protected override void OnEquipped()
	{
		base.OnEquipped();
		SetDropped( false );
	}

	// DrawHud / DrawCrosshair and the per-frame crosshair positioning come from the engine BaseWeapon.

	/// <summary>
	/// Called when added to the player's inventory
	/// </summary>
	/// <param name="player"></param>
	public virtual void OnAdded( Player player )
	{
		// nothing
	}

	/// <summary>
	/// Called every frame, when active
	/// </summary>
	public virtual void OnFrameUpdate( Player player )
	{
		if ( player is null ) return;

		CreateViewModel();

		GameObject.Network.Interpolation = false;
	}

	/// <summary>
	/// Called when setting up the camera - use this to apply effects on the camera based on this carriable
	/// </summary>
	/// <param name="player"></param>
	/// <param name="camera"></param>
	public virtual void OnCameraSetup( Player player, Sandbox.CameraComponent camera )
	{
	}

	/// <summary>
	/// Can directly influence the player's eye angles here
	/// </summary>
	/// <param name="player"></param>
	/// <param name="angles"></param>
	public virtual void OnCameraMove( Player player, ref Angles angles )
	{
	}

	/// <summary>
	/// Spawns this weapon's world pickup and throws it - a fresh clone of its own prefab for weapons
	/// with a <see cref="DroppedWeapon"/> component, otherwise <see cref="ItemPrefab"/>. A fresh clone
	/// avoids carrying the held instance's ownership and state. Returns the pickup, or null when this
	/// weapon has no pickup form. Host only.
	/// </summary>
	public GameObject SpawnDroppedPickup( Vector3 position, Vector3 velocity, Connection owner = null )
	{
		if ( !Networking.IsHost )
			return null;

		GameObject prefab = null;

		if ( GetComponent<DroppedWeapon>( true ).IsValid() )
		{
			var source = GameObject.PrefabInstanceSource;
			if ( !string.IsNullOrEmpty( source ) )
				prefab = GameObject.GetPrefab( source );
		}

		if ( !prefab.IsValid() )
			prefab = ItemPrefab;

		if ( !prefab.IsValid() )
			return null;

		var pickup = prefab.Clone( new CloneConfig { Transform = new Transform( position ), StartEnabled = true } );

		Ownable.Set( pickup, owner );
		pickup.Tags.Add( "removable" );
		pickup.NetworkSpawn();

		if ( pickup.GetComponent<Rigidbody>() is { } rb )
		{
			rb.Velocity = velocity;
			rb.AngularVelocity = Vector3.Random * 8.0f;
		}

		return pickup;
	}

	/// <summary>
	/// Is this item currently being used? When true, prevents auto-switching away on item pickup etc.
	/// </summary>
	public virtual bool IsInUse()
	{
		return false;
	}

	public virtual void OnPlayerDeath( PlayerDiedParams args )
	{
	}
}
