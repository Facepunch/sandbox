using Sandbox.CameraNoise;
using Sandbox.Movement;

/// <summary>
/// Holds player information like health
/// </summary>
public sealed partial class Player : Component, Component.IDamageable, PlayerController.IEvents, ISaveEvents, IKillSource
{
	private static Player Local { get; set; }
	public static Player FindLocalPlayer() => Local;
	public static T FindLocalWeapon<T>() where T : BaseCarryable => FindLocalPlayer()?.GetComponentInChildren<T>( true );
	public static T FindLocalToolMode<T>() where T : ToolMode => FindLocalPlayer()?.GetComponentInChildren<T>( true );

	[RequireComponent] public PlayerController Controller { get; set; }
	[Property] public GameObject Body { get; set; }
	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] public float Health { get; set; } = 100;
	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] public float MaxHealth { get; set; } = 100;

	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] public float Armour { get; set; } = 0;
	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] public float MaxArmour { get; set; } = 100;

	[Sync( SyncFlags.FromHost )] public PlayerData PlayerData { get; set; }

	public Transform EyeTransform
	{
		get
		{
			if ( !Controller.IsValid() )
			{
				Log.Warning( $"Invalid Controller for {this.GameObject}" );
				return default;
			}
			return Controller.EyeTransform;
		}
	}

	public bool IsLocalPlayer => !IsProxy;
	public Guid PlayerId => PlayerData.IsValid() ? PlayerData.PlayerId : Guid.Empty;
	public long SteamId => PlayerData.IsValid() ? PlayerData.SteamId : 0;
	public string DisplayName => PlayerData.IsValid() ? PlayerData.DisplayName : "Unknown";

	// IKillSource
	string IKillSource.DisplayName => DisplayName;
	long IKillSource.SteamId => SteamId;
	void IKillSource.OnKill( GameObject victim )
	{
		PlayerData.Kills++;
		PlayerData.AddStat( victim?.GetComponent<Player>().IsValid() ?? false ? "kills" : "kills.npc" );
	}

	/// <summary>
	/// True if the player wants the HUD not to draw right now
	/// </summary>
	public bool WantsHideHud
	{
		get
		{
			var freeCam = Scene.Get<FreeCamGameObjectSystem>();
			if ( freeCam.IsActive )
				return true;

			var weapon = GetComponent<PlayerInventory>()?.ActiveWeapon;
			if ( weapon.IsValid() && weapon.WantsHideHud )
				return true;

			return false;
		}
	}

	protected override void OnStart()
	{
		if ( IsLocalPlayer )
			Local = this;

		var targets = Scene.GetAllComponents<DeathCameraTarget>()
			.Where( x => x.Connection == Network.Owner );

		// We don't care about spectating corpses once we spawn
		foreach ( var t in targets )
		{
			t.GameObject.Destroy();
		}
	}

	protected override void OnDestroy()
	{
		if ( Local == this )
			Local = null;
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

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	void CreateRagdoll()
	{
		if ( !Controller.Renderer.IsValid() )
			return;

		var go = new GameObject( true, "Ragdoll" );
		go.Tags.Add( "ragdoll" );
		go.WorldTransform = WorldTransform;

		var mainBody = go.Components.Create<SkinnedModelRenderer>();
		mainBody.CopyFrom( Controller.Renderer );
		mainBody.UseAnimGraph = false;

		// copy the clothes
		foreach ( var clothing in Controller.Renderer.GameObject.Children.Where( x => x.Tags.Has( "clothing" ) ).SelectMany( x => x.Components.GetAll<SkinnedModelRenderer>() ) )
		{
			if ( !clothing.IsValid() ) continue;

			var newClothing = new GameObject( true, clothing.GameObject.Name );
			newClothing.Parent = go;

			var item = newClothing.Components.Create<SkinnedModelRenderer>();
			item.CopyFrom( clothing );
			item.BoneMergeTarget = mainBody;
		}

		var physics = go.Components.Create<ModelPhysics>();
		physics.Model = mainBody.Model;
		physics.Renderer = mainBody;
		physics.CopyBonesFrom( Controller.Renderer, true );

		var corpse = go.AddComponent<DeathCameraTarget>();
		corpse.Connection = Network.Owner;
		corpse.Created = DateTime.Now;

		CopyBoneScalesToRagdoll( go );
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
		Sound.Play( "sounds/flatline.sound" );
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
		PlayerData?.MarkForRespawn();
		Ghost();
		GameObject.Destroy();
	}

	[Rpc.Host]
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

	private RealTimeSince _timeSinceJumpPressed;

	void OnControl()
	{
		if ( Input.UsingController )
		{
			Controller.UseInputControls = !(Input.Down( "SpawnMenu" ) || Input.Down( "InspectMenu" ));
		}
		else
		{
			Controller.UseInputControls = true;
		}

		if ( Input.Pressed( "die" ) )
		{
			KillSelf();
			return;
		}

		if ( Input.Pressed( "jump" ) )
		{
			if ( _timeSinceJumpPressed < 0.3f )
			{
				if ( GetComponent<NoclipMoveMode>( true ) is { } noclip )
				{
					noclip.Enabled = !noclip.Enabled;
				}
			}

			_timeSinceJumpPressed = 0;
		}

		if ( Input.Pressed( "undo" ) )
		{
			ConsoleSystem.Run( "undo" );
		}

		GetComponent<PlayerInventory>()?.OnControl();

		Scene.Get<Inventory>()?.HandleInput();
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
		if ( !PlayerData.IsValid() ) return;
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

	void PlayerController.IEvents.OnLanded( float distance, Vector3 impactVelocity )
	{
		IPlayerEvent.PostToGameObject( GameObject, x => x.OnLand( distance, impactVelocity ) );

		var player = Components.Get<Player>();
		if ( !player.IsValid() ) return;

		if ( Controller.ThirdPerson || !player.IsLocalPlayer ) return;

		new Punch( new Vector3( 0.3f * distance, Random.Shared.Float( -1, 1 ), Random.Shared.Float( -1, 1 ) ), 1.0f, 1.5f, 0.7f );
	}

	void PlayerController.IEvents.OnJumped()
	{
		IPlayerEvent.PostToGameObject( GameObject, x => x.OnJump() );

		var player = Components.Get<Player>();

		if ( Controller.ThirdPerson || !player.IsLocalPlayer ) return;

		new Punch( new Vector3( -20, 0, 0 ), 0.5f, 2.0f, 1.0f );
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

	void ISaveEvents.AfterLoad( string filename )
	{
		if ( !Body.IsValid() ) return;

		var dresser = Body.GetComponentInChildren<Dresser>( true );
		if ( !dresser.IsValid() ) return;

		// Apply clothing after load
		_ = ReapplyClothingAfterLoad( dresser );
	}

	private async Task ReapplyClothingAfterLoad( Dresser dresser )
	{
		await dresser.Apply();
		GameObject.Network.Refresh();
	}
}
