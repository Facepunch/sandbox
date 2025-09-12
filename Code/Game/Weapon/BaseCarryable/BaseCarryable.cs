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
	[Property, Feature( "Inventory" ), Range( 0, 4 )] public int InventorySlot { get; set; } = 0;
	[Property, Feature( "Inventory" )] public int InventoryOrder { get; set; } = 0;
	[Property, Feature( "Inventory" )] public string DisplayName { get; set; } = "My Weapon";
	[Property, Feature( "Inventory" ), TextArea] public Texture DisplayIcon { get; set; }

	public GameObject ViewModel { get; protected set; }
	public GameObject WorldModel { get; protected set; }

	/// <summary>
	/// Wether this weapon should be avoided when determining an item to swap to
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
			if ( !go.IsValid() ) return null;

			return go.GetComponent<WeaponModel>();
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

	/// <summary>
	/// Where shoot effects come from. Either the point on the world model or the viewmodel, whichever is currently being used.
	/// </summary>
	public GameObject MuzzleTransform
	{
		get
		{
			return WeaponModel?.MuzzleTransform ?? GameObject;
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

		controller.Renderer.Set( "holdtype", (int)HoldType );

		if ( player.IsLocalPlayer )
		{
			if ( Scene.Camera is null )
				return;

			var hud = Scene.Camera.Hud;

			var aimPos = Screen.Size * 0.5f;

			if ( controller.ThirdPerson )
			{
				var tr = Scene.Trace.Ray( controller.EyeTransform.ForwardRay, 4096 )
									.IgnoreGameObjectHierarchy( controller.GameObject )
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
	/// Called when this is pulled out
	/// </summary>
	public virtual void OnEquipped( Player player )
	{

	}

	/// <summary>
	/// Called when this is put away
	/// </summary>
	public virtual void OnHolstered( Player player )
	{

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

		GameObject.NetworkInterpolation = false;
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

		if ( !Owner.IsValid() )
			return;

		var damagable = attack.Target.GetComponentInParent<IDamageable>();
		if ( damagable is not null )
		{
			var info = new DamageInfo( attack.Damage, Owner.GameObject, GameObject );
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
