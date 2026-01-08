using Sandbox.CameraNoise;
using Sandbox.Rendering;
using Sandbox.UI.Inventory;

/// <summary>
/// Holds player information like health
/// </summary>
public sealed partial class Player : Component, Component.IDamageable, PlayerController.IEvents
{
	public static Player FindLocalPlayer() => Game.ActiveScene.GetAllComponents<Player>().Where( x => x.IsLocalPlayer ).FirstOrDefault();
	public static T FindLocalWeapon<T>() where T : BaseCarryable => FindLocalPlayer()?.GetComponentInChildren<T>( true );
	public static T FindLocalToolMode<T>() where T : ToolMode => FindLocalPlayer()?.GetComponentInChildren<T>( true );

	[RequireComponent] public PlayerController Controller { get; set; }
	[Property] public GameObject Body { get; set; }
	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] public float Health { get; set; } = 100;
	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] public float MaxHealth { get; set; } = 100;

	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] public float Armour { get; set; } = 0;
	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] public float MaxArmour { get; set; } = 100;

	[Sync( SyncFlags.FromHost )] public PlayerData PlayerData { get; set; }

	[Header( "Icons" )]
	[Property] public Texture HealthIcon { get; set; }
	[Property] public Texture ArmourIcon { get; set; }


	public Transform EyeTransform
	{
		get
		{
			Assert.True( Controller.IsValid(), $"Player {DisplayName}'s PlayerController is invalid (IsValid: {this.IsValid()}, IsLocalPlayer: {IsLocalPlayer}, IsHost: {Networking.IsHost}, IsActive: {PlayerData?.Connection?.IsActive})" );
			return Controller.EyeTransform;
		}
	}
	public bool IsLocalPlayer => !IsProxy;
	public Guid PlayerId => PlayerData.PlayerId;
	public long SteamId => PlayerData.SteamId;
	public string DisplayName => PlayerData.DisplayName;

	/// <summary>
	/// True if the player wants the HUD not to draw right now
	/// </summary>
	public bool WantsHideHud
	{
		get
		{
			var weapon = GetComponent<PlayerInventory>()?.ActiveWeapon;
			if ( weapon.IsValid() && weapon.WantsHideHud )
				return true;

			return false;
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !IsProxy )
		{
			ControlSpray();
		}
	}

	protected override void OnStart()
	{
		var targets = Scene.GetAllComponents<DeathCameraTarget>()
			.Where( x => x.Connection == Network.Owner );

		// We don't care about spectating corpses once we spawn
		foreach ( var t in targets )
		{
			t.GameObject.Destroy();
		}
	}

	/// <summary>
	/// Try to inherit transforms from the player onto its new ragdoll
	/// </summary>
	/// <param name="ragdoll"></param>
	private void CopyBoneScalesToRagdoll( GameObject ragdoll )
	{
		// we are only interested in the bones of the player, not anything that may be attached to it.
		var playerRenderer = Body.GetComponent<SkinnedModelRenderer>();
		var bones = playerRenderer.Model.Bones;

		var ragdollRenderer = ragdoll.GetComponent<SkinnedModelRenderer>();
		ragdollRenderer.CreateBoneObjects = true;

		var ragdollObjects = ragdoll.GetAllObjects( true ).ToLookup( x => x.Name );

		foreach ( var bone in bones.AllBones )
		{
			var boneName = bone.Name;

			if ( !ragdollObjects.Contains( boneName ) )
				continue;

			var boneObject = playerRenderer.GetBoneObject( boneName );
			if ( !boneObject.IsValid() )
			{
				continue;
			}

			var boneOnRagdoll = ragdollObjects[boneName].FirstOrDefault();

			if ( boneOnRagdoll.IsValid() && boneObject.WorldScale != Vector3.One )
			{
				boneOnRagdoll.Flags = boneOnRagdoll.Flags.WithFlag( GameObjectFlags.ProceduralBone, true );
				boneOnRagdoll.WorldScale = boneObject.WorldScale;

				var z = boneOnRagdoll.Parent;
				z.Flags = z.Flags.WithFlag( GameObjectFlags.ProceduralBone, true );
				z.WorldScale = boneObject.WorldScale;
			}
		}
	}

	/// <summary>
	/// Creates a ragdoll but it isn't enabled
	/// </summary>
	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	void CreateRagdoll()
	{
		if ( Application.IsDedicatedServer ) return;

		var ragdoll = Controller.CreateRagdoll();
		if ( !ragdoll.IsValid() ) return;

		CopyBoneScalesToRagdoll( ragdoll );

		var corpse = ragdoll.AddComponent<DeathCameraTarget>();
		corpse.Connection = Network.Owner;
		corpse.Created = DateTime.Now;
	}

	void CreateRagdollAndGhost()
	{
		var go = new GameObject( false, "Observer" );
		go.Components.Create<PlayerObserver>();
		go.NetworkSpawn( Network.Owner );
	}

	/// <summary>
	/// Broadcasts death to other players
	/// </summary>
	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	void NotifyDeath( IPlayerEvent.DiedParams args )
	{
		IPlayerEvent.PostToGameObject( GameObject, x => x.OnDied( args ) );

		if ( args.Attacker == GameObject )
		{
			IPlayerEvent.PostToGameObject( GameObject, x => x.OnSuicide() );
		}
	}

	[Rpc.Owner( NetFlags.HostOnly )]
	private void Flatline()
	{
		Sound.Play( "audio/sounds/flatline.sound" );
	}

	private void Ghost()
	{
		CreateRagdollAndGhost();
	}

	/// <summary>
	/// Called on the host when a player dies
	/// </summary>
	void Kill( in DamageInfo d )
	{
		//
		// Play the flatline sound on the owner
		//
		if ( IsLocalPlayer )
		{
			Flatline();
		}

		//
		// Let everyone know about the death
		//

		NotifyDeath( new IPlayerEvent.DiedParams() { Attacker = d.Attacker } );

		var inventory = GetComponent<PlayerInventory>();
		if ( inventory.IsValid() )
		{
			inventory.SwitchWeapon( null );
			inventory.DropCoffin();
		}

		if ( d.Tags.HasAny( DamageTags.Crush, DamageTags.Explosion, DamageTags.GibAlways ) )
		{
			Gib( d.Position, d.Origin );
		}
		else
		{
			CreateRagdoll();
		}

		//
		// Ghost and say goodbye to the player
		//
		Ghost();
		GameObject.Destroy();
	}

	[Rpc.Owner]
	public void EquipBestWeapon()
	{
		var inventory = GetComponent<PlayerInventory>();

		if ( inventory.IsValid() )
			inventory.SwitchWeapon( inventory.GetBestWeapon() );
	}

	void PlayerController.IEvents.PreInput()
	{
		OnControl();
	}

	RealTimeSince timeSinceJumpPressed;

	void OnControl()
	{
		Scene.Get<Inventory>()?.HandleInputOpen();

		if ( Input.Pressed( "die" ) )
		{
			KillSelf();
			return;
		}

		if ( Input.Pressed( "jump" ) )
		{
			if ( timeSinceJumpPressed < 0.3f )
			{
				if ( GetComponent<NoclipMoveMode>( true ) is { } noclip )
				{
					noclip.Enabled = !noclip.Enabled;
				}
			}

			timeSinceJumpPressed = 0;
		}

		if ( Input.Pressed( "undo" ) )
		{
			ConsoleSystem.Run( "undo" );
		}

		GetComponent<PlayerInventory>()?.OnControl();

		Scene.Get<Inventory>()?.HandleInput();

		if ( Scene.Camera.RenderExcludeTags.Contains( "ui" ) )
			return;

		if ( !WantsHideHud )
		{
			var hud = Scene.Camera.Hud;
			DrawVitals( hud, Screen.Size * new Vector2( 0.1f, 0.9f ) );
		}
	}

	[ConCmd( "sbdm.dev.sethp", ConVarFlags.Cheat )]
	private static void Dev_SetHp( int hp )
	{
		FindLocalPlayer().Health = hp;
	}

	private SoundHandle _dmgSound;

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void NotifyOnDamage( IPlayerEvent.DamageParams args )
	{
		IPlayerEvent.PostToGameObject( GameObject, x => x.OnDamage( args ) );

		Effects.Current.SpawnBlood( args.Position, (args.Origin - args.Position).Normal, args.Damage );

		if ( IsLocalPlayer )
		{
			_dmgSound?.Stop();

			if ( args.Tags.Contains( DamageTags.Shock ) )
			{
				_dmgSound = Sound.Play( "damage_taken_shock" );
			}
			else
			{
				_dmgSound = Sound.Play( "damage_taken_shot" );
			}
		}
	}

	public void OnDamage( in DamageInfo dmg )
	{
		if ( Health < 1 ) return;
		if ( PlayerData.IsGodMode ) return;

		//
		// Ignore impact damage from the world, for now
		//
		if ( dmg.Tags.Contains( "impact" ) )
		{
			// Was this fall damage? If so, we can bail out here
			if ( Controller.Velocity.Dot( Vector3.Down ) > 10 )
				return;

			// We were hit by some flying object, or flew into a wall, 
			// so lets take that damage.
		}


		var damage = dmg.Damage;
		if ( dmg.Tags.Contains( DamageTags.Headshot ) )
			damage *= 2;

		if ( Armour > 0 )
		{
			float remainingDamage = damage - Armour;
			Armour = Math.Max( 0, Armour - damage );
			damage = Math.Max( 0, remainingDamage );
		}

		Health -= damage;

		NotifyOnDamage( new IPlayerEvent.DamageParams()
		{
			Damage = damage,
			Attacker = dmg.Attacker,
			Weapon = dmg.Weapon,
			Tags = dmg.Tags,
			Position = dmg.Position,
			Origin = dmg.Origin,
		} );

		// We didn't die
		if ( Health >= 1 ) return;

		GameManager.Current.OnDeath( this, dmg );

		Health = 0;
		Kill( dmg );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void Gib( Vector3 hitPos, Vector3 origin )
	{
		var gibList = new List<PlayerGib>( GetComponentsInChildren<PlayerGib>( true ) );

		DeathCameraTarget target = null;
		foreach ( var g in gibList )
		{
			// Death camera target is the first gib
			if ( !target.IsValid() )
			{
				target = g.AddComponent<DeathCameraTarget>();
				target.Connection = Network.Owner;
				target.Created = DateTime.Now;
			}

			g.Gib( origin, hitPos, noShrink: true );
		}

		Effects.Current.SpawnBlood( WorldPosition, Vector3.Up, 500.0f );
	}

	void PlayerController.IEvents.OnEyeAngles( ref Angles ang )
	{
		var player = Components.Get<Player>();
		var angles = ang;
		IPlayerEvent.Post( x => x.OnCameraMove( ref angles ) );
		ang = angles;
	}

	void PlayerController.IEvents.PostCameraSetup( CameraComponent camera )
	{
		// Set up initial field of view from preferences
		camera.FovAxis = CameraComponent.Axis.Vertical;
		camera.FieldOfView = Screen.CreateVerticalFieldOfView( Preferences.FieldOfView, 9.0f / 16.0f );

		IPlayerEvent.Post( x => x.OnCameraSetup( camera ) );

		ApplyMovementCameraEffects( camera );

		IPlayerEvent.Post( x => x.OnCameraPostSetup( camera ) );
	}

	float roll;
	private void ApplyMovementCameraEffects( CameraComponent camera )
	{
		if ( Controller.ThirdPerson ) return;
		if ( !GamePreferences.ViewBobbing ) return;

		var scaler = Controller.WishVelocity.Length.Remap( 0, Controller.RunSpeed, 0, 1 );

		// side movement
		var r = Controller.WishVelocity.Dot( EyeTransform.Left ) / -250.0f;
		roll = MathX.Lerp( roll, r, Time.Delta * 10.0f, true );

		camera.WorldRotation *= new Angles( 0, 0, roll );
	}

	void PlayerController.IEvents.OnLanded( float distance, Vector3 impactVelocity )
	{
		IPlayerEvent.PostToGameObject( GameObject, x => x.OnLand( distance, impactVelocity ) );

		var player = Components.Get<Player>();
		if ( !player.IsValid() ) return;

		if ( Controller.ThirdPerson || !player.IsLocalPlayer ) return;

		new Punch( new Vector3( 0.3f * distance, Random.Shared.Float( -1, 1 ), Random.Shared.Float( -1, 1 ) ), 1.0f, 1.5f, 0.7f );
	}

	bool noPickupNotices = false;
	public IDisposable NoNoticeScope()
	{
		noPickupNotices = true;
		return new Sandbox.Utility.DisposeAction( () => noPickupNotices = false );
	}

	public void ShowNotice( string message )
	{
		if ( noPickupNotices ) return;
		NotifyNotice( message );
	}

	[Rpc.Owner]
	public void NotifyNotice( string message )
	{
		if ( !IsLocalPlayer ) return;

		Log.Info( $"you picked up {message}" );
		//Scene.RunEvent<Sandbox.UI.Notices>( x => x.Display( message ) );
	}

	void PlayerController.IEvents.OnJumped()
	{
		IPlayerEvent.PostToGameObject( GameObject, x => x.OnJump() );

		var player = Components.Get<Player>();

		if ( Controller.ThirdPerson || !player.IsLocalPlayer ) return;

		new Punch( new Vector3( -20, 0, 0 ), 0.5f, 2.0f, 1.0f );
	}

	public void DrawVitals( HudPainter hud, Vector2 bottomleft )
	{
		hud.DrawHudElement( $"{Health.CeilToInt()}", bottomleft, HealthIcon, 30f );

		if ( Armour > 0f )
		{
			hud.DrawHudElement( $"{Armour.CeilToInt()}", bottomleft - new Vector2( 0, 64f * Hud.Scale ), ArmourIcon, 30f );
		}
	}

	public T GetWeapon<T>() where T : BaseCarryable
	{
		return GetComponent<PlayerInventory>().GetWeapon<T>();
	}

	public void SwitchWeapon<T>() where T : BaseCarryable
	{
		var weapon = GetWeapon<T>();
		if ( weapon == null ) return;

		GetComponent<PlayerInventory>().SwitchWeapon( weapon );
	}


	public override void OnParentDestroy()
	{
		// When parent is destroyed, unparent the player to avoid destroying it
		GameObject.SetParent( null, true );
	}
}
