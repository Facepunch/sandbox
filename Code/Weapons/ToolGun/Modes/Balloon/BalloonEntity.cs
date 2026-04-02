public class BalloonEntity : Component, Component.IDamageable
{
	[Property] public PrefabFile PopEffect { get; set; }
	[Property] public SoundEvent PopSound { get; set; }
	[RequireComponent] public Prop Prop { get; set; }

	void IDamageable.OnDamage( in DamageInfo damage )
	{
		if ( IsProxy ) return;

		Pop();
	}

	[Rpc.Host]
	private void Pop()
	{
		if ( PopEffect.IsValid() )
		{
			var effect = GameObject.Clone( PopEffect, new CloneConfig { Transform = WorldTransform, StartEnabled = false } );

			foreach ( var tintable in effect.GetComponentsInChildren<ITintable>( true ) )
			{
				tintable.Color = Prop.Tint;
			}

			effect.NetworkSpawn( true, null );
		}

		if ( PopSound is null )
		{
			PopSound = ResourceLibrary.Get<SoundEvent>( "entities/balloon/sounds/balloon_pop.sound" );
		}

		if ( PopSound is not null )
		{
			Sound.Play( PopSound, WorldPosition );
		}

		GameObject.Destroy();
	}
}
