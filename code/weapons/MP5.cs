using Sandbox;

[Spawnable]
[Library( "weapon_mp5", Title = "MP5" )]
partial class MP5 : Weapon
{
	public override float PrimaryRate => 15.0f;
	public override float SecondaryRate => 1.0f;
	public override float ReloadTime => 2.5f;
	private ParticleSystem EjectBrass;

	public override void Spawn()
	{
		base.Spawn();

		Model = Cloud.Model( "https://asset.party/facepunch/w_mp5" );
		LocalScale = 1.5f;
	}

	public override void ActiveStart( Entity ent )
	{
		base.ActiveStart( ent );
		EjectBrass = Cloud.ParticleSystem( "https://asset.party/facepunch/9mm_ejectbrass" );
	}

	public override void CreateViewModel()
	{
		ViewModelEntity = new ViewModel();
		ViewModelEntity.Position = Position;
		ViewModelEntity.Owner = Owner;
		ViewModelEntity.EnableViewmodelRendering = true;
		ViewModelEntity.Model = Cloud.Model( "https://asset.party/facepunch/v_mp5" );

		var arms = new AnimatedEntity( "models/first_person/first_person_arms.vmdl" );
		arms.SetParent( ViewModelEntity, true );
		arms.EnableViewmodelRendering = true;
	}

	public override void AttackPrimary()
	{
		TimeSincePrimaryAttack = 0;
		TimeSinceSecondaryAttack = 0;

		(Owner as AnimatedEntity)?.SetAnimParameter( "b_attack", true );
		ViewModelEntity?.SetAnimParameter( "b_attack", true );

		//
		// Tell the clients to play the shoot effects
		//
		ShootEffects();
		PlaySound( "rust_smg.shoot" );

		//
		// Shoot the bullets
		//
		ShootBullet( 0.1f, 1.5f, 5.0f, 3.0f );
	}

	public override void Simulate( IClient player )
	{
		base.Simulate( player );

		var attack_hold = !IsReloading && Input.Down( "attack1" ) ? 1.0f : 0.0f;
		(Owner as AnimatedEntity)?.SetAnimParameter( "attack_hold", attack_hold );
		ViewModelEntity?.SetAnimParameter( "attack_hold", attack_hold );
	}

	[ClientRpc]
	protected override void ShootEffects()
	{
		Game.AssertClient();

		Particles.Create( "particles/pistol_muzzleflash.vpcf", EffectEntity, "muzzle" );
		Particles.Create( EjectBrass.ResourcePath, EffectEntity, "eject" );
	}

	public override void SimulateAnimator( CitizenAnimationHelper anim )
	{
		anim.HoldType = CitizenAnimationHelper.HoldTypes.Rifle;
		anim.Handedness = CitizenAnimationHelper.Hand.Both;
		anim.AimBodyWeight = 1.0f;
	}
}
