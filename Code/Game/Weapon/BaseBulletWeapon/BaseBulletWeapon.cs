public partial class BaseBulletWeapon : BaseWeapon
{
	[Property]
	public SoundEvent ShootSound { get; set; }

	protected TimeSince TimeSinceShoot = 0f;
	private bool queuedShot = false;

	/// <summary>
	/// Useful utility method that queues an input, for guns like pistols if you shoot too infrequently it'll feel like the fire rate is inconsistent.
	/// </summary>
	/// <param name="inputCheck"></param>
	/// <param name="fireRate"></param>
	/// <returns></returns>
	protected bool IsInputQueued( Func<bool> inputCheck, float fireRate )
	{
		if ( inputCheck.Invoke() )
		{
			if ( TimeSinceShoot >= fireRate )
			{
				return true;
			}
			else
			{
				queuedShot = true;
			}
		}

		if ( queuedShot && TimeSinceShoot >= fireRate )
		{
			TimeSinceShoot = 0f;
			queuedShot = false;
			return true;
		}

		return false;
	}

	[Rpc.Broadcast]
	public void ShootEffects( Vector3 hitpoint, bool hit, Vector3 normal, GameObject hitObject, Surface hitSurface, Vector3? origin = null, bool noEvents = false )
	{
		if ( Application.IsDedicatedServer ) return;

		if ( !Owner.IsValid() )
			return;

		Owner.Controller.Renderer.Set( "b_attack", true );

		if ( !noEvents )
		{
			var ev = new IWeaponEvent.AttackEvent( ViewModel.IsValid() );
			IWeaponEvent.PostToGameObject( GameObject.Root, x => x.OnAttack( ev ) );
			IWeaponEvent.PostToGameObject( GameObject.Root, x => x.CreateRangedEffects( this, hitpoint, origin ) );

			if ( ShootSound.IsValid() )
			{
				var snd = GameObject.PlaySound( ShootSound );
				// If we're shooting, the sound should not be spatialized
				if ( Owner.IsLocalPlayer && snd.IsValid() )
				{
					snd.SpacialBlend = 0;
				}
			}
		}

		if ( hit )
		{
			var impactObjects = SurfaceImpacts.FindForResourceOrDefault( hitSurface );
			if ( impactObjects is null )
				return;

			if ( impactObjects.BulletImpact is not null )
			{
				var impact = impactObjects.BulletImpact.Clone();
				impact.WorldPosition = hitpoint + normal;
				impact.WorldRotation = Rotation.LookAt( normal );
				impact.SetParent( hitObject, true );
			}

			if ( impactObjects.BulletDecal is not null )
			{
				var decal = impactObjects.BulletDecal.Clone();
				decal.WorldPosition = hitpoint + normal;
				decal.WorldRotation = Rotation.LookAt( -normal );
				decal.WorldScale = 1;
				decal.Parent = Scene;
				decal.SetParent( hitObject, true );
			}
		}
	}
}
