
public abstract class Constraint : ToolMode
{
	SelectionPoint _point1;
	SelectionPoint _point2;
	int stage = 0;

	public override void OnControl()
	{
		base.OnControl();

		if ( Input.Pressed( "attack1" ) )
		{
			var select = TraceSelect();

			if ( !select.IsValid() )
				return;

			if ( stage == 0 )
			{
				_point1 = select;
				stage++;
				ShootEffects( select );
				return;
			}

			if ( stage == 1 )
			{
				_point2 = select;

				Create( _point1, _point2 );
				ShootEffects( select );
			}

			stage = 0;
		}
	}

	[Rpc.Host]
	private void Create( SelectionPoint point1, SelectionPoint point2 )
	{
		CreateConstraint( point1, point2 );
	}

	protected abstract void CreateConstraint( SelectionPoint point1, SelectionPoint point2 );
}
