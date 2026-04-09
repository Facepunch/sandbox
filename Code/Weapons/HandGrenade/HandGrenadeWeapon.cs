using Sandbox.Rendering;

public enum ThrowType
{
	Far = 0,
	Near = 1
}

/// <summary>
/// A throwable grenade weapon
/// Cooks while held — explodes in hand if held too long
/// </summary>
public sealed class HandGrenadeWeapon : BaseWeapon
{
	[Property] public GameObject Prefab { get; set; }
	[Property] public float ThrowPower { get; set; } = 1200f;

	/// <summary>
	/// Fuse time in seconds — grenade explodes after this, whether thrown or not.
	/// </summary>
	[Property] public float Lifetime { get; set; } = 3f;

	/// <summary>
	/// Explosion damage radius.
	/// </summary>
	[Property] public float Radius { get; set; } = 256f;

	/// <summary>
	/// Maximum damage at the center.
	/// </summary>
	[Property] public float MaxDamage { get; set; } = 125f;

	/// <summary>
	/// Physics force scale for the explosion.
	/// </summary>
	[Property] public float Force { get; set; } = 1f;

	[Sync] TimeSince TimeSinceCooked { get; set; }
	[Sync] bool IsCooking { get; set; }
	[Sync] bool IsThrowing { get; set; }
	[Sync] TimeUntil TimeUntilThrown { get; set; }

	ThrowType CurrentThrowType { get; set; } = ThrowType.Far;
	float ThrowBlend { get; set; }

	public override bool IsInUse() => IsCooking;

	protected override void OnEnabled()
	{
		base.OnEnabled();
		AddShootDelay( 0.5f );
	}

	public override void OnPlayerDeath( IPlayerEvent.DiedParams args )
	{
		if ( !IsCooking ) return;

		// Drop the grenade at your feet
		if ( HasOwner )
			Throw( Owner, Vector3.Down, 0.2f );
	}

	public override void OnControl()
	{
		if ( ShootInput.Pressed() )
		{
			Log.Info( $"Dorp grenade" );
			DropGrenade();
		}
	}

	public override void OnControl( Player player )
	{
		// Wait for throw animation to finish
		if ( IsThrowing )
		{
			if ( TimeUntilThrown )
			{
				IsThrowing = false;

				if ( !HasAmmo() )
				{
					SwitchToBestWeapon();
					DestroyGameObject();
					return;
				}

				// Deploy next grenade
				WeaponModel?.Renderer?.Set( "b_deploy_new", true );
			}

			return;
		}

		// Start cooking on press
		if ( !IsCooking && CanPrimaryAttack() && (Input.Pressed( "Attack1" ) || Input.Pressed( "Attack2" )) )
		{
			IsCooking = true;
			TimeSinceCooked = 0;

			WeaponModel?.Renderer?.Set( "b_charge", true );
			WeaponModel?.Renderer?.Set( "charge_type", 0 );
		}

		if ( !IsCooking )
			return;

		// Update throw direction blend
		UpdateThrowType();

		// Cooked too long — explode in hand
		if ( TimeSinceCooked > Lifetime )
		{
			IsCooking = false;
			TakeAmmo( 1 );
			ExplodeInHand();

			if ( !HasAmmo() )
			{
				SwitchToBestWeapon();
				DestroyGameObject();
			}

			return;
		}

		// Release both buttons to throw
		if ( !Input.Down( "Attack1" ) && !Input.Down( "Attack2" ) )
		{
			Throw( player );
		}
	}

	void UpdateThrowType()
	{
		bool attack1 = Input.Down( "Attack1" );
		bool attack2 = Input.Down( "Attack2" );

		float target = (attack1 && attack2) ? 0.5f : attack2 ? 1.0f : 0.0f;
		ThrowBlend = ThrowBlend.LerpTo( target, Time.Delta * 3.0f );
		CurrentThrowType = ThrowBlend < 0.4f ? ThrowType.Far : ThrowType.Near;

		WeaponModel?.Renderer?.Set( "throw_blend", ThrowBlend );
		WeaponModel?.Renderer?.Set( "throw_type", (int)CurrentThrowType );
	}

	void Throw( Player player, Vector3? overrideDirection = null, float powerScale = 1f )
	{
		IsCooking = false;

		if ( !TakeAmmo( 1 ) )
		{
			SwitchToBestWeapon();
			DestroyGameObject();
			return;
		}

		var direction = overrideDirection ?? player.EyeTransform.Rotation.Forward;

		if ( !overrideDirection.HasValue && CurrentThrowType == ThrowType.Near )
		{
			direction = (direction + Vector3.Up * 0.3f).Normal;
			powerScale *= 0.5f;
		}

		var startPos = GetThrowPosition( player, direction );

		SpawnProjectile( player, startPos, direction, powerScale );

		// Play throw animation
		WeaponModel?.Renderer?.Set( "b_charge", false );
		WeaponModel?.Renderer?.Set( "b_attack", true );

		AddShootDelay( 1f );
		IsThrowing = true;
		TimeUntilThrown = 0.5f;
	}

	Vector3 GetThrowPosition( Player player, Vector3 direction )
	{
		var eye = player.EyeTransform;
		var right = eye.Rotation.Right;
		var forward = direction;

		var target = eye.Position + forward * 18f + right * 8f;

		var tr = Scene.Trace.Box( BBox.FromPositionAndSize( Vector3.Zero, 8f ), eye.Position, target )
			.WithoutTags( "trigger", "ragdoll" )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.Run();

		return tr.Hit ? tr.EndPosition : target;
	}

	[Rpc.Host]
	void DropGrenade()
	{
		if ( !Prefab.IsValid() ) return;

		var go = Prefab.Clone( WorldPosition );

		var explosive = go.GetOrAddComponent<TimedExplosive>();
		if ( explosive.IsValid() )
		{
			explosive.Lifetime = Lifetime;
			explosive.Radius = Radius;
			explosive.Damage = MaxDamage;
			explosive.Force = Force;
		}

		// Don't collide with the weapon we dropped from
		var filter = go.AddComponent<PhysicsFilter>();
		filter.Body = GameObject;

		// No velocity — just drops in place
		go.NetworkSpawn();
	}

	[Rpc.Host]
	void SpawnProjectile( Player player, Vector3 startPos, Vector3 direction, float powerScale )
	{
		if ( !player.IsValid() ) return;
		if ( !Prefab.IsValid() ) return;

		var go = Prefab.Clone( startPos );

		// Configure the timed explosive with remaining fuse
		var explosive = go.GetOrAddComponent<TimedExplosive>();
		if ( explosive.IsValid() )
		{
			explosive.Lifetime = MathF.Max( 0.1f, Lifetime - TimeSinceCooked );
			explosive.Radius = Radius;
			explosive.Damage = MaxDamage;
			explosive.Force = Force;
		}

		var rb = go.GetComponent<Rigidbody>();
		if ( rb.IsValid() )
		{
			var baseVelocity = player.GetComponent<PlayerController>().Velocity;
			rb.Velocity = baseVelocity + direction * (ThrowPower * powerScale) + Vector3.Up * 100f;
			rb.AngularVelocity = go.WorldRotation.Right * 10f;
		}

		// Don't collide with the weapon we threw from
		var filter = go.AddComponent<PhysicsFilter>();
		filter.Body = GameObject;

		go.NetworkSpawn();
	}

	void SwitchToBestWeapon()
	{
		var inventory = Owner?.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() ) return;

		var best = inventory.GetBestWeapon();
		if ( best.IsValid() )
			inventory.SwitchWeapon( best );
	}

	[Rpc.Host]
	void ExplodeInHand()
	{
		// Spawn the explosion directly
		var explosionPrefab = ResourceLibrary.Get<PrefabFile>( "/prefabs/engine/explosion_med.prefab" );
		if ( !explosionPrefab.IsValid() )
			return;

		var explosionPos = Owner.IsValid() ? Owner.EyeTransform.Position : WorldPosition;
		var explosion = GameObject.Clone( explosionPrefab, new CloneConfig { Transform = new Transform( explosionPos ), StartEnabled = false } );
		if ( !explosion.IsValid() )
			return;

		explosion.RunEvent<RadiusDamage>( x =>
		{
			x.Radius = Radius;
			x.PhysicsForceScale = Force;
			x.DamageAmount = MaxDamage;
			x.Attacker = explosion;
		}, FindMode.EverythingInSelfAndDescendants );

		explosion.Enabled = true;
		explosion.NetworkSpawn( true, null );

		SwitchToBestWeapon();
		DestroyGameObject();
	}

	public override void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var color = !HasAmmo() ? CrosshairNoShoot : CrosshairCanShoot;
		hud.SetBlendMode( BlendMode.Lighten );
		hud.DrawCircle( center, 6, color );
	}
}
