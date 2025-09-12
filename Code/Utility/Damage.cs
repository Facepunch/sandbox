public static class Damage
{
	[Rpc.Broadcast]
	private static void ApplyForce( Rigidbody rb, Vector3 point, Vector3 force )
	{
		if ( !rb.IsValid() ) return;
		if ( rb.Network.Owner is null ) return;
		if ( !rb.Network.Owner.Id.Equals( Connection.Local.Id ) ) return;

		rb.ApplyForceAt( point, force );
	}

	public static void Radius( Vector3 point, float radius, float baseDamage, TagSet tags, GameObject source = null, GameObject weapon = null, Curve falloff = default, Guid instigatorId = default, GameObject ignore = null, float extraForce = 0 )
	{
		Assert.True( Networking.IsHost, "Only the host can deal damage!" );
		if ( !Networking.IsHost ) return;

		if ( falloff.Frames.IsDefaultOrEmpty )
		{
			falloff = new Curve( new Curve.Frame( 0.0f, 1.0f ), new Curve.Frame( 1.0f, 0.0f ) );
		}

		var scene = Game.ActiveScene;
		if ( !scene.IsValid() ) return;

		var objectsInArea = scene.FindInPhysics( new Sphere( point, radius ) );

		var losTrace = scene.Trace.WithTag( "map" ).WithoutTags( "trigger" );

		if ( weapon.IsValid() )
			losTrace = losTrace.IgnoreGameObjectHierarchy( weapon );

		foreach ( var rb in objectsInArea.SelectMany( x => x.GetComponents<Rigidbody>() ).Distinct() )
		{
			if ( !rb.MotionEnabled )
				continue;

			if ( ignore.IsValid() && ignore.IsDescendant( rb.GameObject ) )
				continue;

			// If the object isn't in line of sight, fuck it off
			var tr = losTrace.Ray( point, rb.WorldPosition ).Run();
			if ( tr.Hit && tr.GameObject.IsValid() )
			{
				if ( !rb.GameObject.Root.IsDescendant( tr.GameObject ) )
					continue;
			}

			var dir = (rb.WorldPosition - point).Normal;
			var distance = rb.WorldPosition.Distance( point );

			// Bullshit, we should pass a force through or invent a scale from damage
			var forceMagnitude = Math.Clamp( 10000000000f / (distance * distance + 1), 0, 10000000000f );

			forceMagnitude += extraForce * (1 - (distance / radius));

			ApplyForce( rb, point, dir * forceMagnitude );
		}

		foreach ( var damageable in objectsInArea.SelectMany( x => x.GetComponentsInParent<Component.IDamageable>().Distinct() ) )
		{
			var target = damageable as Component;

			if ( ignore.IsValid() && ignore.IsDescendant( target.GameObject ) )
				continue;

			// If the object isn't in line of sight, fuck it off
			var tr = losTrace.Ray( point, target.WorldPosition ).Run();
			if ( tr.Hit && tr.GameObject.IsValid() )
			{
				if ( !target.GameObject.Root.IsDescendant( tr.GameObject ) )
					continue;
			}

			var distance = target.WorldPosition.Distance( point );
			var damage = baseDamage * falloff.Evaluate( distance / radius );
			var direction = (target.WorldPosition - point).Normal;
			var force = direction * distance * 50f;

			var position = tr.HitPosition;
			if ( target.GetComponentInChildren<CapsuleCollider>() is { } collider )
			{
				// for dramatic effect, lets just say hit the centre of the guy
				position += collider.End / 2;
			}

			var damageInfo = new DamageInfo( damage, source, weapon )
			{
				Origin = point,
				Position = position,
				Tags = tags
			};

			damageable.OnDamage( damageInfo );
		}
	}
}
