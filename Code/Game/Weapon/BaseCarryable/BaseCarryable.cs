using Sandbox.Rendering;

/// <summary>
/// Info about a trace attack. It's a struct so we can add to it without updating params everywhere.
/// </summary>
/// <param name="Target"></param>
/// <param name="Damage"></param>
/// <param name="Tags"></param>
/// <param name="Position"></param>
/// <param name="Origin"></param>
/// <param name="Hitbox"></param>
public record struct TraceAttackInfo( GameObject Target, float Damage, TagSet Tags = null, Vector3 Position = default, Vector3 Origin = default )
{
	/// <summary>
	/// Constructs a <see cref="TraceAttackInfo"/> from a trace and input damage.
	/// </summary>
	public static TraceAttackInfo From( SceneTraceResult tr, float damage, TagSet tags = default, bool localise = true )
	{
		tags ??= new();

		if ( localise && tr.Hitbox?.Tags is not null )
		{
			tags.Add( tr.Hitbox?.Tags );
		}

		return new TraceAttackInfo( tr.GameObject, damage, tags, tr.HitPosition, tr.StartPosition );
	}
}

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

			if ( Scene.Camera.RenderExcludeTags.Contains( "firstperson" ) ) go = default;

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
	/// The effective attacker to use in damage attribution.
	/// Returns the owning player's GameObject if held, the seated player's GameObject if
	/// controlled from a contraption seat, or this weapon's own GameObject as a last resort.
	/// </summary>
	protected GameObject EffectiveAttacker
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

	protected override void OnUpdate()
	{
		var player = Owner;
		var controller = player?.Controller;
		if ( controller is null ) return;

		if ( player.IsLocalPlayer )
		{
			if ( Scene.Camera is null )
				return;

			var hud = Scene.Camera.Hud;

			var aimPos = Screen.Size * 0.5f;

			if ( controller.ThirdPerson )
			{
				var tr = Scene.Trace.Ray( AimRay, 4096 )
									.IgnoreGameObjectHierarchy( AimIgnoreRoot )
									.Run();

				aimPos = Scene.Camera.PointToScreenPixels( tr.EndPosition );
			}

			if ( !Scene.Camera.RenderExcludeTags.Has( "ui" ) )
			{
				DrawHud( hud, aimPos );
			}
		}
	}

	public virtual void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		// nothing
	}

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
	/// Called every frame, on the owning player's client.
	/// </summary>
	public virtual void OnPlayerUpdate( Player player )
	{
		Assert.True( !IsProxy );

		try
		{
			OnControl( player );
		}
		catch ( System.Exception e )
		{
			Log.Error( e, $"{GetType().Name}.OnControl {e.Message}" );
		}
	}

	/// <summary>
	/// Called every update, scoped to the owning player
	/// </summary>
	/// <param name="player"></param>
	public virtual void OnControl( Player player )
	{
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
	/// Run a trace related attack with some set information.
	/// This is targeted to the host who then does things.
	/// </summary>
	/// <param name="attack"></param>
	[Rpc.Host]
	public void TraceAttack( TraceAttackInfo attack )
	{
		if ( !attack.Target.IsValid() )
			return;

		// Use owner as attacker when held by a player, seated player when controlled from a
		// contraption seat, or fall back to the weapon itself (standalone/world weapon)
		var attacker = EffectiveAttacker;

		var dmg = attack.Target.GetComponentInParent<IDamageable>();
		if ( dmg is not null )
		{
			var info = new DamageInfo( attack.Damage, attacker, GameObject )
			{
				Position = attack.Position,
				Origin = attack.Origin,
				Tags = attack.Tags
			};

			dmg.OnDamage( info );
		}

		if ( attack.Target.GetComponentInChildren<Rigidbody>() is var rb && rb.IsValid() )
		{
			// TODO: Scale this based on damage?
			rb.ApplyImpulseAt( attack.Position, Vector3.Direction( attack.Origin, attack.Position ) * rb.Mass * 100 );
		}
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
