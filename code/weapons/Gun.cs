using Sandbox;

[Library( "gun" )]
partial class Gun : Weapon
{
	public override string ViewModelPath => "weapons/rust_pistol/v_rust_pistol.vmdl";
	public override float PrimaryRate => 10;

	public TimeSince TimeSinceDischarge { get; set; }

	public override void Spawn()
	{
		base.Spawn();

		SetModel( "weapons/rust_pistol/rust_pistol.vmdl" );
	}

	/// <summary>
	/// Lets make primary attack semi automatic
	/// </summary>
	public override bool CanPrimaryAttack()
	{
		if ( !Owner.Input.Pressed( InputButton.Attack1 ) )
			return false;

		return base.CanPrimaryAttack();
	}

	public override void Reload()
	{
		base.Reload();

		ViewModelEntity?.SetAnimParam( "reload", true );
	}

	public override void AttackPrimary()
	{
		TimeSincePrimaryAttack = 0;
		TimeSinceSecondaryAttack = 0;

		Shoot( Owner.EyePos, Owner.EyeRot.Forward );
	}

	private void Shoot( Vector3 pos, Vector3 dir )
	{
		//
		// Tell the clients to play the shoot effects
		//
		ShootEffects();


		bool InWater = Physics.TestPointContents( pos, CollisionLayer.Water );
		var forward = dir * (InWater ? 500 : 4000);

		//
		// ShootBullet is coded in a way where we can have bullets pass through shit
		// or bounce off shit, in which case it'll return multiple results
		//
		foreach ( var tr in TraceBullet( pos, pos + forward ) )
		{
			tr.Surface.DoBulletImpact( tr );

			if ( !IsServer ) continue;
			if ( !tr.Entity.IsValid() ) continue;

			//
			// We turn prediction off for this, so any exploding effects only
			// happen once
			//
			using ( Prediction.Off() )
			{
				var damage = DamageInfo.FromBullet( tr.EndPos, forward.Normal * 1000, 100 )
					.UsingTraceResult( tr )
					.WithAttacker( Owner )
					.WithWeapon( this );

				tr.Entity.TakeDamage( damage );
			}
		}
	}

	private void Discharge()
	{
		if ( TimeSinceDischarge < 0.5f )
			return;

		TimeSinceDischarge = 0;

		var muzzle = GetAttachment( "muzzle" );
		var pos = muzzle.Pos;
		var rot = muzzle.Rot;
		Shoot( pos, rot.Forward );

		ApplyAbsoluteImpulse( rot.Backward * 200.0f );
	}

	protected override void OnPhysicsCollision( CollisionEventData eventData )
	{
		if ( eventData.Speed > 500.0f )
		{
			Discharge();
		}
	}

	public override void OnPlayerControlTick( Player owner )
	{
		base.OnPlayerControlTick( owner );

		//DebugTrace( owner );

		//if ( !NavMesh.IsLoaded )
		//	return;

		//var forward = owner.EyeRot.Forward * 2000;


		//var tr = Trace.Ray( owner.EyePos, owner.EyePos + forward )
		//				.Ignore( owner )
		//				.Run();

		//var closestPoint = NavMesh.GetClosestPoint( tr.EndPos );

		//DebugOverlay.Line( tr.EndPos, closestPoint, 0.1f );

		//DebugOverlay.Axis( closestPoint, Rotation.LookAt( tr.Normal ), 2.0f, Time.Delta * 2 );
		//DebugOverlay.Text( closestPoint, $"CLOSEST Walkable POINT", Time.Delta * 2 );

		//NavMesh.BuildPath( Owner.WorldPos, closestPoint );
	}

	//public void DebugTrace( Player player )
	//{
	//	for ( float x = -10; x < 10; x += 1.0f )
	//		for ( float y = -10; y < 10; y += 1.0f )
	//		{
	//			var tr = Trace.Ray( player.EyePos, player.EyePos + player.EyeRot.Forward * 4096 + player.EyeRot.Left * (x + Rand.Float( -1.6f, 1.6f )) * 100 + player.EyeRot.Up * (y + Rand.Float( -1.6f, 1.6f )) * 100 ).Ignore( player ).Run();

	//			if ( IsServer ) DebugOverlay.Line( tr.EndPos, tr.EndPos + tr.Normal, Color.Cyan, duration: 20 );
	//			else DebugOverlay.Line( tr.EndPos, tr.EndPos + tr.Normal, Color.Yellow, duration: 20 );
	//		}
	//}

	[ClientRpc]
	public virtual void ShootEffects()
	{
		Host.AssertClient();

		var muzzle = EffectEntity.GetAttachment( "muzzle" );
		//bool InWater = Physics.TestPointContents( muzzle.Pos, CollisionLayer.Water );

		Sound.FromEntity( "rust_pistol.shoot", this );
		Particles.Create( "particles/pistol_muzzleflash.vpcf", EffectEntity, "muzzle" );

		ViewModelEntity?.SetAnimParam( "fire", true );
		CrosshairPanel?.OnEvent( "onattack" );

		if ( Owner == Player.Local )
		{
			new Sandbox.ScreenShake.Perlin( 0.5f, 2.0f, 0.5f );
		}
	}
}
