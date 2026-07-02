using Sandbox.Rendering;

public partial class BaseSandboxWeapon : Sandbox.BaseWeapon, IKillIcon, IPlayerControllable
{
	// DisplayName, DisplayIcon, Value, Slot, ViewModel and WorldModel are all inherited from the
	// engine BaseWeapon / BaseInventoryItem. (DisplayIcon also satisfies IKillIcon.)

	// MuzzleGameObject is inherited from the engine BaseWeapon.

	/// <summary>
	/// Used for overriding the display icon
	/// </summary>
	public virtual string InventoryIconOverride => null;

	/// <summary>
	/// If true the game should hide the hud when holding this weapon. Useful for cameras, or scopes.
	/// </summary>
	public virtual bool WantsHideHud => false;

	// WeaponModel resolution (view model when drawn, else world model, else own hierarchy) comes from
	// the engine BaseWeapon.

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


	// DrawHud / DrawCrosshair and the per-frame crosshair positioning come from the engine BaseWeapon.

	/// <summary>
	/// Runs on the host when added to an inventory. Seeds the shared reserve pool from the ammo
	/// resource on first pickup of this ammo type; the engine base seeds the magazines - only when
	/// they've never been filled, so a dropped gun keeps its rounds through a pickup.
	/// </summary>
	protected override void OnAdded( Sandbox.InventoryComponent inventory )
	{
		// Seeded before the base so the resource's default wins over the engine's StartingAmmo-only
		// seeding. Guarding on the pool being empty stops two guns that share a resource from
		// double-seeding it (and pickup churn from inflating reserve over time).
		if ( UsesAmmo && AmmoType is not null && !inventory.HasAmmo( AmmoType.ResourcePath ) )
		{
			var seed = AmmoType.DefaultStartingAmmo + StartingAmmo;
			if ( seed > 0 )
				AddReserveAmmo( seed );
		}

		// In an inventory - not a world pickup any more.
		IsDropped = false;

		base.OnAdded( inventory );
	}

	//
	// Seat / contraption control (IPlayerControllable) - a weapon mounted on a contraption fires from
	// the driver's inputs.
	//

	/// <summary>The input that fires the primary attack when this weapon is controlled via a seat.</summary>
	[Property, Sync, ClientEditable, Group( "Inputs" )] public ClientInput ShootInput { get; set; }

	/// <summary>The input that fires the secondary attack when this weapon is controlled via a seat.</summary>
	[Property, Sync, ClientEditable, Group( "Inputs" )] public ClientInput SecondaryInput { get; set; }

	/// <summary>A seated player can only control this while not holding a weapon of their own.</summary>
	public virtual bool CanControl( Player player )
	{
		var inventory = player.GetComponent<PlayerInventory>();
		return inventory is null || !inventory.ActiveWeapon.IsValid();
	}

	public virtual void OnStartControl() { }

	public virtual void OnEndControl() { }

	// Explicit interface impl so it doesn't clash with the engine's held-item OnControl pump;
	// subclasses override OnSeatControl to change the seated behaviour.
	void IPlayerControllable.OnControl() => OnSeatControl();

	protected virtual void OnSeatControl()
	{
		if ( HasOwner ) return;
		// Seat fire is fully host-authoritative - the host reads the driver's synced ClientInput and
		// runs the shot for real, damage applying directly (no hit claims). The driving client doesn't
		// run the attack at all.
		if ( !Networking.IsHost ) return;

		if ( ShootInput.Down() && CanPrimaryAttack() )
			PrimaryAttack();

		if ( SecondaryInput.Down() && CanSecondaryAttack() )
			SecondaryAttack();
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
	/// Drops this weapon into the world as a pickup - the same live object, so it keeps its state
	/// (ammo, tool modes). Unparents it, drops network ownership, enables the dropped components and
	/// tags it cleanable. Host only.
	/// </summary>
	public void DropIntoWorld( Vector3 position, Vector3 velocity, Connection owner = null )
	{
		if ( !Networking.IsHost )
			return;

		GameObject.SetParent( null, true );
		GameObject.Enabled = true;
		WorldPosition = position;
		Slot = -1;

		Network.DropOwnership();

		Ownable.Set( GameObject, owner );
		GameObject.Tags.Add( "removable" );

		IsDropped = true;

		if ( GetComponent<Rigidbody>( true ) is { } body )
		{
			body.Velocity = velocity;
			body.AngularVelocity = Vector3.Random * 8.0f;
		}
	}

	/// <summary>
	/// The engine inventory is dropping this - throw the live object out in front of the dropper.
	/// </summary>
	protected override bool OnDrop()
	{
		var player = Owner;

		if ( player.IsValid() )
		{
			var eye = player.EyeTransform;
			var velocity = (player.Controller.IsValid() ? player.Controller.Velocity : Vector3.Zero)
				+ eye.Forward * 200f + Vector3.Up * 100f;

			DropIntoWorld( eye.Position + eye.Forward * 48f, velocity, player.Network.Owner );
		}
		else
		{
			DropIntoWorld( WorldPosition, Vector3.Up * 50f );
		}

		return true;
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
