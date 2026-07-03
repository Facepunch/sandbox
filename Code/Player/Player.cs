
/// <summary>
/// Holds player information like health
/// </summary>
public sealed partial class Player : Component, Component.IDamageable, PlayerController.IEvents, Global.ISaveEvents, IKillSource
{
	[RequireComponent] 
	public PlayerController Controller { get; set; }

	[Property] 
	public GameObject Body { get; internal set; }

	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] 
	public float Health { get; set; } = 100;

	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] 
	public float MaxHealth { get; set; } = 100;

	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] 
	public float Armour { get; set; } = 0;

	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] 
	public float MaxArmour { get; set; } = 100;

	[Sync( SyncFlags.FromHost )] 
	public PlayerData PlayerData { get; internal set; }

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

	string IKillSource.DisplayName => Network.Owner?.DisplayName ?? "Unknown";
	long IKillSource.SteamId => (long)(Network.Owner?.SteamId ?? default);
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
		_timeSinceSpawned = 0;

		if ( IsLocalPlayer )
			LocalPlayer = this;

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
		if ( LocalPlayer == this )
			LocalPlayer = null;
	}

	/// <summary>
	/// Whether this player is currently noclipping. Synced so proxies can animate correctly.
	/// </summary>
	[Sync]
	public bool IsNoclipping { get; internal set; }

	/// <summary>Seconds of invulnerability after spawning, to stop spawn-killing. 0 disables it.</summary>
	[ConVar( "sb.spawnprotection", ConVarFlags.Replicated | ConVarFlags.Saved, Help = "Seconds of invulnerability after spawning. 0 disables spawn protection." )]
	public static float SpawnProtection { get; set; } = 5.0f;

	/// <summary>
	/// Time since this player last spawned. Drives <see cref="IsSpawnProtected"/>.
	/// </summary>
	private RealTimeSince _timeSinceSpawned;

	/// <summary>
	/// True while the player is briefly invulnerable just after spawning, per the
	/// <c>sb.spawnprotection</c> convar.
	/// </summary>
	public bool IsSpawnProtected => _timeSinceSpawned < SpawnProtection;

	protected override void OnUpdate()
	{
		if ( Controller.IsValid() && Controller.Renderer.IsValid() )
		{
			Controller.Renderer.Set( "b_noclip", IsNoclipping );
		}

		// Seated weapon bookkeeping and HUD paint in the update stage - the camera modifier
		// (Player.Camera) only composes the view.
		if ( IsLocalPlayer )
		{
			UpdateSeatedWeapons();
			DrawSeatedWeaponHud();
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
	/// Calculates the launch velocity for a ragdoll based on the damage source.
	/// For explosions, uses the direction from the blast origin to this NPC.
	/// Otherwise, falls back to the attacker's physical velocity.
	/// </summary>
	Vector3 GetDeathLaunchVelocity( in DamageInfo damage )
	{
		if ( damage.Tags.Contains( DamageTags.Explosion ) && damage.Origin != Vector3.Zero )
		{
			var dist = (WorldPosition - damage.Origin).Length;
			var strength = MathX.Remap( dist, 0, 512, 1024, 2048, true );

			var dir = (WorldPosition - damage.Origin).Normal;
			dir += Vector3.Up * 1.0f;
			dir = dir.Normal;

			return dir * strength;
		}

		return 0;
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	void CreateRagdoll( Vector3 velocity, Vector3 origin )
	{
		if ( !Controller.Renderer.IsValid() || Controller.Renderer.Model is null )
			return;

		var batch = Scene.BatchGroup();

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
		batch.Dispose();

		physics.CopyBonesFrom( Controller.Renderer, true );

		var corpse = go.AddComponent<DeathCameraTarget>();
		corpse.Connection = Network.Owner;
		corpse.Created = DateTime.Now;

		CopyBoneScalesToRagdoll( go );

		ApplyRagdollForce( physics, velocity, origin );
	}

	void ApplyRagdollForce( ModelPhysics physics, Vector3 force, Vector3 origin )
	{
		if ( !physics.IsValid() ) return;
		if ( force.Length < 1 ) return;

		foreach ( var body in physics.Bodies )
		{
			var rb = body.Component;
			if ( !rb.IsValid() ) continue;
			rb.ApplyImpulse( Vector3.Direction( origin, rb.WorldPosition ) * force.Length * rb.Mass );
		}
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
	void NotifyDeath( PlayerDiedParams args )
	{
		Local.IPlayerEvents.PostToGameObject( GameObject, x => x.OnDied( args ) );
		Global.IPlayerEvents.Post( x => x.OnPlayerDied( this, args ) );

		if ( args.Attacker == GameObject )
		{
			Local.IPlayerEvents.PostToGameObject( GameObject, x => x.OnSuicide() );
			Global.IPlayerEvents.Post( x => x.OnPlayerSuicide( this ) );
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

		NotifyDeath( new PlayerDiedParams() { Attacker = d.Attacker } );

		var inventory = GetComponent<PlayerInventory>();
		if ( inventory.IsValid() )
		{
			inventory.SwitchWeapon( null );
		}

		CreateRagdoll( GetDeathLaunchVelocity( d ), d.Origin );

		//
		// Ghost and say goodbye to the player
		//
		Ghost();
		GameObject.Destroy();
	}

	[Rpc.Host]
	internal void EquipBestWeapon()
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

		if ( Input.Pressed( "noclip" ) )
		{
			ToggleNoclip();
		}

		if ( Input.Pressed( "jump" ) )
		{
			if ( _timeSinceJumpPressed < 0.3f )
			{
				ToggleNoclip();
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

	void ToggleNoclip()
	{
		if ( GetComponent<NoclipMoveMode>( true ) is { } noclip )
		{
			noclip.Enabled = !noclip.Enabled;
			IsNoclipping = noclip.Enabled;
		}
	}

	private SoundHandle _dmgSound;

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void NotifyOnDamage( PlayerDamageParams args )
	{
		Local.IPlayerEvents.PostToGameObject( GameObject, x => x.OnDamage( args ) );
		Global.IPlayerEvents.Post( x => x.OnPlayerDamage( this, args ) );

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
		if ( IsSpawnProtected ) return;

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

		// Fire pre-damage event — listeners can modify damage or cancel
		var damageEvent = new PlayerDamageEvent { Player = this, DamageInfo = dmg, Damage = dmg.Damage };
		Local.IPlayerEvents.PostToGameObject( GameObject, x => x.OnDamaging( damageEvent ) );
		Global.IPlayerEvents.Post( x => x.OnPlayerDamaging( damageEvent ) );

		if ( damageEvent.Cancelled )
			return;

		var damage = damageEvent.Damage;
		if ( dmg.Tags.Contains( DamageTags.Headshot ) )
			damage *= 2;

		if ( Armour > 0 )
		{
			float remainingDamage = damage - Armour;
			Armour = Math.Max( 0, Armour - damage );
			damage = Math.Max( 0, remainingDamage );
		}

		Health -= damage;

		NotifyOnDamage( new PlayerDamageParams()
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

	void PlayerController.IEvents.OnLanded( float distance, Vector3 impactVelocity )
	{
		Local.IPlayerEvents.PostToGameObject( GameObject, x => x.OnLand( distance, impactVelocity ) );
		Global.IPlayerEvents.Post( x => x.OnPlayerLanded( this, distance, impactVelocity ) );

		var player = Components.Get<Player>();
		if ( !player.IsValid() ) return;

		if ( Controller.ThirdPerson || !player.IsLocalPlayer ) return;

		// Landing thump - pitch dips with the fall, bounces back.
		Scene.Camera?.AddPunch( new Angles( 0.3f * distance, Random.Shared.Float( -1, 1 ), Random.Shared.Float( -1, 1 ) ), 1f, 1f );
	}

	void PlayerController.IEvents.OnJumped()
	{
		Local.IPlayerEvents.PostToGameObject( GameObject, x => x.OnJump() );
		Global.IPlayerEvents.Post( x => x.OnPlayerJumped( this ) );

		if ( Controller.ThirdPerson || !IsLocalPlayer ) return;

		Scene.Camera?.AddPunch( new Angles( -20f, 0f, 0f ), 0.7f, 0.5f );
	}

	public T GetWeapon<T>() where T : BaseSandboxWeapon
	{
		return GetComponent<PlayerInventory>().GetWeapon<T>();
	}

	public void SwitchWeapon<T>() where T : BaseSandboxWeapon
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

	void Global.ISaveEvents.AfterLoad( string filename )
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
