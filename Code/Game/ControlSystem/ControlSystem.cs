public class ControlSystem : GameObjectSystem<ControlSystem>
{
	// When each chair first became occupied. Used to sort seats so the earliest occupant is in charge
	private readonly Dictionary<BaseChair, RealTimeSince> _occupiedSince = new();

	public ControlSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartFixedUpdate, 10, OnTick, "ControlSystem" );
	}

	void OnTick()
	{
		// TODO this should be more generic, some kind of interface?
		var driven = new HashSet<GameObject>();

		foreach ( var chair in GetSortedSeats() )
		{
			var builder = new LinkedGameObjectBuilder();
			builder.AddConnected( chair.GameObject );

			// Skip if a seat occupied earlier already claimed this
			if ( builder.Objects.Any( driven.Contains ) ) continue;
			driven.UnionWith( builder.Objects );

			RunControl( chair, builder );
		}
	}

	IEnumerable<BaseChair> GetSortedSeats()
	{
		var chairs = Scene.GetAll<BaseChair>();

		foreach ( var chair in chairs )
		{
			if ( !chair.IsValid() || !chair.IsOccupied )
				_occupiedSince.Remove( chair );
			else
				_occupiedSince.TryAdd( chair, 0 );
		}

		return chairs
			.Where( c => c.IsValid() && c.IsOccupied )
			.OrderBy( c => (float)_occupiedSince.GetValueOrDefault( c, default ) );
	}

	void RunControl( BaseChair chair, LinkedGameObjectBuilder builder )
	{
		var player = chair.GetOccupant();
		if ( !player.IsValid() ) return;

		using var scope = ClientInput.PushScope( player );

		foreach ( var o in builder.Objects )
		{
			foreach ( var controllable in o.GetComponentsInChildren<IPlayerControllable>() )
			{
				if ( controllable is null ) continue;
				if ( !controllable.CanControl( player ) ) continue;

				controllable.OnControl();
			}
		}
	}
}
