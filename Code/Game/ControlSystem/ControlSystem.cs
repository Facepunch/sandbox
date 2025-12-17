public class ControlSystem : GameObjectSystem<ControlSystem>
{
	public ControlSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartFixedUpdate, 10, OnTick, "ControlSystem" );
	}

	void OnTick()
	{
		// TODO this should be more generic, some kind of interface?
		foreach ( var chair in Scene.GetAll<BaseChair>() )
		{
			if ( !chair.IsValid() ) continue;
			RunControl( chair );
		}
	}

	void RunControl( BaseChair chair )
	{
		if ( !chair.IsOccupied ) return;

		var player = chair.GetOccupant();
		if ( !player.IsValid() ) return;

		var builder = new LinkedGameObjectBuilder();
		builder.AddConnected( chair.GameObject );

		using var scope = ClientInput.PushScope( player );

		foreach ( var o in builder.Objects )
		{
			var controllable = o.GetComponent<IPlayerControllable>();
			if ( controllable is null ) continue;

			controllable.OnControl();
		}

	}
}
