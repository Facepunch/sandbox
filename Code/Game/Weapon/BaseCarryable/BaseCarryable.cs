using Sandbox.Rendering;

/// <summary>
/// Info about a trace attack. It's a struct so we can add to it without updating params everywhere.
/// </summary>
/// <param name="Target"></param>
/// <param name="Damage"></param>
/// <param name="Tags"></param>
/// <param name="Position"></param>
/// <param name="Origin"></param>
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

public partial class BaseCarryable : Component, IKillIcon
{
	[Property, Feature( "Inventory" )] public string DisplayName { get; set; } = "My Weapon";
	[Property, Feature( "Inventory" ), TextArea] public Texture DisplayIcon { get; set; }

	/// <summary>
	/// The prefab to spawn in the world when this item is dropped from the inventory.
	/// </summary>
	[Property, Feature( "Inventory" )] public GameObject ItemPrefab { get; set; }

	public GameObject ViewModel { get; protected set; }
	public GameObject WorldModel { get; protected set; }

	/// <summary>
	/// Optional explicit muzzle point. Used when no WeaponModel is present (e.g. standalone/seat mode).
	/// If unset, falls back to the WeaponModel muzzle or the weapon's own GameObject.
	/// </summary>
	[Property] public GameObject MuzzleGameObject { get; set; }

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
	/// The value of this weapon, used for auto-switch.
	/// </summary>
	[Property, Feature( "Inventory" )] public int Value { get; set; } = 0;

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
	public Player Owner
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
	/// Unified aim ray for all weapons. Returns the correct ray based on context:
	/// first-person held, third-person held, seated (targeted or muzzle), or standalone.
	/// </summary>
	public Ray AimRay
	{
		get
		{
			if ( HasOwner )
			{
				var owner = Owner;
				if ( owner.Controller.IsValid() && owner.Controller.ThirdPerson && Scene.Camera.IsValid() )
					return Scene.Camera.Transform.World.ForwardRay;

				return owner.EyeTransform.ForwardRay;
			}

			var seated = ClientInput.Current;
			if ( seated.IsValid() && IsTargetedAim && Scene.Camera.IsValid() )
				return Scene.Camera.Transform.World.ForwardRay;

			var muzzle = MuzzleTransform.WorldTransform;
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
			return GameObject;
		}
	}

	/// <summary>
	/// Where shoot effects come from. Either the point on the world model or the viewmodel, whichever is currently being used.
	/// </summary>
	public GameObject MuzzleTransform
	{
		get
		{
			if ( WeaponModel?.MuzzleTransform.IsValid() ?? false ) return WeaponModel.MuzzleTransform;
			if ( MuzzleGameObject.IsValid() ) return MuzzleGameObject;
			return GameObject;
		}
	}

	/// <summary>
	/// The inventory slot this item is assigned to, or -1 if unassigned.
	/// Set at runtime when picked up.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public int InventorySlot { get; set; } = -1;

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

	protected override void OnEnabled()
	{
		CreateWorldModel();
	}

	protected override void OnDisabled()
	{
		DestroyWorldModel();
		DestroyViewModel();
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

		if ( !player.Controller.ThirdPerson )
		{
			CreateViewModel();
		}
		else
		{
			DestroyViewModel();
		}

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

		var damagable = attack.Target.GetComponentInParent<IDamageable>();
		if ( damagable is not null )
		{
			var info = new DamageInfo( attack.Damage, attacker, GameObject );
			info.Position = attack.Position;
			info.Origin = attack.Origin;
			info.Tags = attack.Tags;

			damagable.OnDamage( info );
		}

		if ( attack.Target.GetComponentInChildren<Rigidbody>() is var rb && rb.IsValid() )
		{
			rb.ApplyForce( (attack.Position - attack.Origin) * 1000f );
		}
	}

	/// <summary>
	/// Is this item currently being used? When true, prevents auto-switching away on item pickup etc.
	/// </summary>
	public virtual bool IsInUse()
	{
		return false;
	}

	public virtual void OnPlayerDeath( IPlayerEvent.DiedParams args )
	{
	}
}
