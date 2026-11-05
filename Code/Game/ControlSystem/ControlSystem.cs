/// <summary>
/// Lets players drive contraptions from a seat. Every tick, this finds who's sitting in each
/// chair and calls <see cref="IPlayerControllable.OnControl"/> on the attached contraption.
/// </summary>
public sealed class ControlSystem : GameObjectSystem<ControlSystem>
{
	private readonly Dictionary<BaseChair, RealTimeSince> _occupiedSince = new();
	private readonly List<BaseChair> _chairs = new();
	private readonly List<BaseChair> _seats = new();
	private readonly HashSet<GameObject> _driven = new();

	public ControlSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartFixedUpdate, 10, OnTick, "ControlSystem" );
	}

	private void OnTick()
	{
		if ( Scene != Game.ActiveScene ) return;

		if ( !Networking.IsHost ) return;

		// Whoever sat down first claims the contraption — a second seat on the same vehicle does nothing.
		var driven = _driven;
		driven.Clear();

		foreach ( var chair in GetSortedSeats() )
		{
			if ( driven.Contains( chair.GameObject.Root ) ) continue;

			var linked = new LinkedGameObjectBuilder();
			linked.AddConnected( chair.GameObject );

			if ( linked.Objects.Any( driven.Contains ) ) continue;
			driven.UnionWith( linked.Objects );

			RunControl( chair, linked );
		}
	}

	private List<BaseChair> GetSortedSeats()
	{
		_chairs.Clear();
		_seats.Clear();

		Scene.GetAll( _chairs );

		foreach ( var chair in _chairs )
		{
			if ( !chair.IsValid() || !chair.IsOccupied )
			{
				_occupiedSince.Remove( chair );
				continue;
			}

			_occupiedSince.TryAdd( chair, 0 );

			// Insertion keeps it stable, like the OrderBy it replaced
			var index = _seats.Count;
			while ( index > 0 && _occupiedSince[_seats[index - 1]] > _occupiedSince[chair] ) index--;
			_seats.Insert( index, chair );
		}

		return _seats;
	}

	private static void RunControl( BaseChair chair, LinkedGameObjectBuilder linked )
	{
		var player = chair.GetOccupant()?.GetComponent<Player>();
		if ( !player.IsValid() ) return;

		var connection = player.Network?.Owner;
		if ( connection is null ) return;

		using var scope = ControlContext.Push( player, connection );

		foreach ( var gameObject in linked.Objects )
		{
			foreach ( var component in gameObject.GetComponentsInChildren<Component>() )
			{
				if ( !component.IsValid() ) continue;
				if ( component is not IPlayerControllable controllable ) continue;
				if ( !controllable.CanControl( player ) ) continue;

				controllable.OnControl();
			}
		}
	}
}
