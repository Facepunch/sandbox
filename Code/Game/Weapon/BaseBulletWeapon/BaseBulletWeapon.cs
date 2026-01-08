public partial class BaseBulletWeapon : BaseWeapon
{
	[Property]
	public SoundEvent ShootSound { get; set; }

	[Rpc.Broadcast]
	public void ShootEffects( Vector3 hitpoint, bool hit, Vector3 normal, GameObject hitObject, Surface hitSurface, Vector3? origin = null, bool noEvents = false )
	{
		if ( Application.IsDedicatedServer ) return;
		if ( !hitSurface.IsValid() ) return;

		Owner?.Controller.Renderer.Set( "b_attack", true );

		if ( !noEvents )
		{
			ViewModel?.RunEvent<ViewModel>( x => x.OnAttack() );
			ViewModel?.RunEvent<ViewModel>( x => x.CreateRangedEffects( this, hitpoint, origin ) );

			if ( ShootSound.IsValid() )
			{
				var snd = GameObject.PlaySound( ShootSound );

				// If we're shooting, the sound should not be spatialized
				if ( Owner.IsValid() && Owner.IsLocalPlayer && snd.IsValid() )
				{
					snd.SpacialBlend = 0;
				}
			}
		}

		if ( hit && hitObject.IsValid() )
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

				if ( hitObject.GetComponentInChildren<SkinnedModelRenderer>() is SkinnedModelRenderer skinned && skinned.CreateBoneObjects )
				{
					// find closest bone
					var bones = skinned.GetBoneTransforms( true );

					float closestDist = float.MaxValue;

					for ( int i = 0; i < bones.Length; i++ )
					{
						var bone = bones[i];
						var dist = bone.Position.Distance( hitpoint );
						if ( dist < closestDist )
						{
							closestDist = dist;
							impact.SetParent( skinned.GetBoneObject( i ), true );
						}
					}
				}
			}
		}
	}


}
