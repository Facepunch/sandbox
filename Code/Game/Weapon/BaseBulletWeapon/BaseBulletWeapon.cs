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
		if ( !hitSurface.IsValid() ) return;

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
			var prefab = hitSurface.PrefabCollection.BulletImpact;
			if ( prefab is null ) prefab = hitSurface.GetBaseSurface()?.PrefabCollection.BulletImpact;

			if ( prefab is not null )
			{
				var fwd = Rotation.LookAt( normal * -1.0f, Vector3.Random );

				var impact = prefab.Clone();
				impact.WorldPosition = hitpoint;
				impact.WorldRotation = fwd;
				impact.SetParent( hitObject, true );
			}
		}
	}
}
