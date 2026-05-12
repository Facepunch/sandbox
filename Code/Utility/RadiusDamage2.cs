namespace Sandbox;

[Category( "Game" ), Icon( "flare" ), EditorHandle( Icon = "💥" )]
public sealed class RadiusDamage2 : Component
{
	[Property]
	public float Radius { get; set; } = 512;

	[Property]
	public float PhysicsForceScale { get; set; } = 1;

	[Property]
	public bool DamageOnEnabled { get; set; } = true;

	[Property]
	public bool Occlusion { get; set; } = true;

	[Property]
	public float DamageAmount { get; set; } = 100;

	[Property]
	public TagSet DamageTags { get; set; } = new TagSet();

	[Property]
	public GameObject Attacker { get; set; }

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( DamageOnEnabled )
		{
			Apply();
		}
	}

	protected override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected )
			return;

		Gizmo.Draw.LineSphere( new Sphere( 0, Radius ), 16 );
	}

	public void Apply()
	{
		var sphere = new Sphere( WorldPosition, Radius );

		var dmg = new DamageInfo();
		dmg.Weapon = GameObject;
		dmg.Damage = DamageAmount;
		dmg.Tags.Add( DamageTags );
		dmg.Attacker = Attacker;

		ApplyDamage( sphere, dmg, PhysicsForceScale, occlusion: Occlusion );
	}

	public static void ApplyDamage( Sphere sphere, DamageInfo damage, float physicsForce = 1, GameObject ignore = null, bool occlusion = true )
	{
		var scene = Game.ActiveScene;
		if ( !scene.IsValid() ) return;

		var point = sphere.Center;
		var damageAmount = damage.Damage;

		//
		// Phase 1: Apply damage first so deaths can spawn ragdolls
		//
		{
			var objectsInArea = scene.FindInPhysics( sphere );
			var estimatedCount = (objectsInArea as ICollection<GameObject>)?.Count ?? 16;
			var damageables = new HashSet<Component.IDamageable>( estimatedCount );
			var rootToIndex = occlusion ? new Dictionary<GameObject, int>( estimatedCount ) : null;
			var rootList = occlusion ? new List<GameObject>( estimatedCount ) : null;

			foreach ( var go in objectsInArea )
			{
				foreach ( var d in go.GetComponentsInParent<Component.IDamageable>() )
				{
					if ( !damageables.Add( d ) ) continue;

					if ( occlusion )
					{
						var root = (d as Component).GameObject.Root;
						if ( rootToIndex.TryAdd( root, rootList.Count ) )
							rootList.Add( root );
					}
				}
			}

			if ( damageables.Count > 0 )
			{
				var traceCount = occlusion ? rootList.Count : 0;
				var passedLos = occlusion ? new bool[traceCount] : null;
				var traceHitPositions = occlusion ? new Vector3[traceCount] : null;

				if ( occlusion && traceCount > 0 )
				{
					var losTrace = scene.PhysicsWorld.Trace.WithTag( "map" ).WithoutTags( "trigger", "gib", "debris", "player" );

					for ( int idx = 0; idx < traceCount; idx++ )
					{
						if ( ignore.IsValid() && ignore.IsDescendant( rootList[idx] ) )
						{
							passedLos[idx] = false;
							continue;
						}

						var tr = losTrace.Ray( point, rootList[idx].WorldPosition ).Run();
						traceHitPositions[idx] = tr.HitPosition;

						var hitObject = tr.Body?.GameObject;
						passedLos[idx] = !tr.Hit || hitObject is null || rootList[idx].IsDescendant( hitObject );
					}
				}

				foreach ( var damageable in damageables )
				{
					var target = damageable as Component;

					if ( ignore.IsValid() && ignore.IsDescendant( target.GameObject ) )
						continue;

					var rootIdx = occlusion ? rootToIndex[target.GameObject.Root] : -1;

					if ( occlusion && !passedLos[rootIdx] )
						continue;

					var distance = target.WorldPosition.Distance( point );
					var distanceFalloff = 1 - (distance / sphere.Radius).Clamp( 0, 1 );

					damage.Damage = damageAmount * distanceFalloff;
					damage.Origin = sphere.Center;
					damage.Position = occlusion ? traceHitPositions[rootIdx] : target.WorldPosition;
					damageable.OnDamage( damage );
				}


			}
		}

		//
		// Phase 2: Apply physics forces with a fresh scan so newly spawned ragdolls are included
		//
		{
			var objectsInArea = scene.FindInPhysics( sphere );
			var rigidbodies = new HashSet<Rigidbody>();
			var rootToIndex = occlusion ? new Dictionary<GameObject, int>() : null;
			var rootList = occlusion ? new List<GameObject>() : null;
			var rootBodyCounts = new Dictionary<GameObject, int>();
			var rootTotalMass = new Dictionary<GameObject, float>();

			foreach ( var go in objectsInArea )
			{
				foreach ( var rb in go.GetComponents<Rigidbody>() )
				{
					if ( rb.IsProxy || !rb.MotionEnabled ) continue;
					if ( !rigidbodies.Add( rb ) ) continue;

					var root = rb.GameObject.Root;
					rootBodyCounts[root] = rootBodyCounts.GetValueOrDefault( root ) + 1;
					rootTotalMass[root] = rootTotalMass.GetValueOrDefault( root ) + rb.Mass;

					if ( occlusion )
					{
						if ( rootToIndex.TryAdd( root, rootList.Count ) )
							rootList.Add( root );
					}
				}
			}

			if ( rigidbodies.Count > 0 )
			{
				var traceCount = occlusion ? rootList.Count : 0;
				var passedLos = occlusion ? new bool[traceCount] : null;

				if ( occlusion && traceCount > 0 )
				{
					var losTrace = scene.PhysicsWorld.Trace.WithTag( "map" ).WithoutTags( "trigger", "gib", "debris", "player" );

					for ( int idx = 0; idx < traceCount; idx++ )
					{
						if ( ignore.IsValid() && ignore.IsDescendant( rootList[idx] ) )
						{
							passedLos[idx] = false;
							continue;
						}

						var tr = losTrace.Ray( point, rootList[idx].WorldPosition ).Run();
						var hitObject = tr.Body?.GameObject;
						passedLos[idx] = !tr.Hit || hitObject is null || rootList[idx].IsDescendant( hitObject );
					}
				}

				foreach ( var rb in rigidbodies )
				{
					if ( ignore.IsValid() && ignore.IsDescendant( rb.GameObject ) )
						continue;

					if ( occlusion && !passedLos[rootToIndex[rb.GameObject.Root]] )
						continue;

					var closest = rb.WorldPosition;
					var direction = closest - point;
					var distance = direction.Length;

					if ( distance > sphere.Radius )
						continue;

					if ( distance.AlmostEqual( 0f ) )
					{
						direction = Vector3.Up;
						distance = 0f;
					}
					else
					{
						direction /= distance;
					}

					var scale = 1f - distance / sphere.Radius;
					var impulse = direction * physicsForce * scale;

					var bodyCount = rootBodyCounts[rb.GameObject.Root];
					if ( bodyCount > 1 )
						impulse *= rb.Mass / rootTotalMass[rb.GameObject.Root];

					rb.ApplyImpulseAt( closest, impulse );
				}
			}
		}

		damage.Damage = damageAmount;
	}
}
