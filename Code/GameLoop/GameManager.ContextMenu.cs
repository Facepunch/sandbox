public sealed partial class GameManager : IContextMenuEvent
{
	/// <summary>
	/// The built-in Inspector right-click options: Ignite/Extinguish, Delete and Break.
	/// Addons add their own by implementing <see cref="IContextMenuEvent"/> the same way.
	/// </summary>
	void IContextMenuEvent.PopulateContextMenu( IContextMenuEvent.Event e )
	{
		var target = e.Target;
		if ( !target.IsValid() ) return;

		var isPlayer = target.Tags.Has( "player" );
		var prop = target.GetComponent<Prop>();

		if ( IsOnFire( target ) )
		{
			e.AddOption( "🧯", Game.Language.GetPhrase( "spawnmenu.inspect.extinguish" ), () => ExtinguishInspectedObject( target ), 100 );
		}
		else if ( !isPlayer )
		{
			e.AddOption( "🔥", Game.Language.GetPhrase( "spawnmenu.inspect.ignite" ), () => IgniteInspectedObject( target ), 100 );
		}

		if ( !isPlayer )
		{
			e.AddOption( "🗑️", Game.Language.GetPhrase( "spawnmenu.inspect.delete" ), () => DeleteInspectedObject( target ), 200 );
		}

		if ( prop.IsValid() && prop.Health > 0 )
		{
			e.AddOption( "💥", Game.Language.GetPhrase( "spawnmenu.inspect.break" ), () => BreakInspectedProp( prop ), 300 );
		}
	}

	/// <summary>
	/// Delete an object from the Inspector context menu.
	/// </summary>
	[Rpc.Host]
	internal static void DeleteInspectedObject( GameObject go )
	{
		if ( !go.IsValid() || go.IsProxy ) return;
		if ( go.Tags.Has( "player" ) ) return;

		// Check ownership if the object has an Ownable component
		if ( !go.HasAccess( Rpc.Caller ) ) return;

		go.Destroy();
	}

	/// <summary>
	/// Break (gib) a prop from the Inspector context menu.
	/// </summary>
	[Rpc.Host]
	internal static void BreakInspectedProp( Prop prop )
	{
		if ( !prop.IsValid() || prop.IsProxy ) return;
		// Check ownership if the object has an Ownable component
		if ( !prop.GameObject.HasAccess( Rpc.Caller ) ) return;

		var damageable = prop.GetComponent<Component.IDamageable>();
		if ( damageable is null ) return;

		var dmg = new DamageInfo( 999999, null, null );
		dmg.Tags.Add( DamageTags.GibAlways );
		damageable.OnDamage( in dmg );
	}

	/// <summary>
	/// Set an object on fire from the Inspector context menu. The engine's FireDamage component
	/// damages every IDamageable under the root until it breaks. Objects that can't break (unbreakable
	/// props, non-damageable entities) just keep burning.
	/// </summary>
	[Rpc.Host]
	internal static void IgniteInspectedObject( GameObject go )
	{
		if ( !go.IsValid() || go.IsProxy ) return;
		if ( go.Tags.Has( "player" ) ) return;
		if ( !go.HasAccess( Rpc.Caller ) ) return;
		if ( IsOnFire( go ) ) return;

		// Props already know how to burn. Prop.IsOnFire is never reset (protected setter), so a prop
		// that was extinguished and re-ignited falls through to the generic path below.
		if ( go.GetComponent<Prop>() is { IsOnFire: false } prop )
		{
			prop.Ignite();
			return;
		}

		// Everything else gets the same fire prefab Prop.Ignite uses, parented to the root
		var firePrefab = ResourceLibrary.Get<PrefabFile>( "/prefabs/engine/ignite.prefab" );
		if ( firePrefab is null )
		{
			Log.Warning( "Can't find /prefabs/engine/ignite.prefab" );
			return;
		}

		var fire = GameObject.Clone( firePrefab, new CloneConfig { Parent = go, Transform = global::Transform.Zero, StartEnabled = true } );
		if ( !fire.IsValid() ) return;

		fire.RunEvent<ParticleModelEmitter>( x => x.Target = go );

		if ( fire.Network.Active )
		{
			fire.Network.Refresh( fire );
		}
	}

	/// <summary>
	/// Put out a burning object from the Inspector context menu. Removes every fire effect
	/// (anything carrying a FireDamage) under the root; descendant destruction is networked.
	/// </summary>
	[Rpc.Host]
	internal static void ExtinguishInspectedObject( GameObject go )
	{
		if ( !go.IsValid() || go.IsProxy ) return;
		if ( !go.HasAccess( Rpc.Caller ) ) return;

		foreach ( var fire in go.GetComponentsInChildren<FireDamage>( true ).ToArray() )
		{
			if ( !fire.IsValid() ) continue;

			var fireObject = fire.GameObject;
			fireObject.Destroy();

			// Destroying a descendant is only broadcast when we refresh it
			if ( go.Network.Active )
			{
				go.Network.Refresh( fireObject );
			}
		}
	}

	/// <summary>
	/// Is this object currently burning? Works on clients too, since the fire prefab is replicated
	/// as a child of the root. Deliberately not Prop.IsOnFire: that flag is never cleared.
	/// </summary>
	internal static bool IsOnFire( GameObject go )
	{
		if ( !go.IsValid() ) return false;
		return go.GetComponentInChildren<FireDamage>( true ).IsValid();
	}
}
