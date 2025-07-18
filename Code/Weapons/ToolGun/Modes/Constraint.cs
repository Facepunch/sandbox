
public abstract class Constraint : ToolMode
{
	protected SelectionPoint Point1;
	protected SelectionPoint Point2;
	protected int Stage = 0;

	public override void OnControl()
	{
		base.OnControl();

		if ( Input.Pressed( "attack1" ) )
		{
			var select = TraceSelect();

			if ( !select.IsValid() )
				return;

			if ( Stage == 0 )
			{
				Point1 = select;
				Stage++;
				ShootEffects( select );
				return;
			}

			if ( Stage == 1 )
			{
				Point2 = select;

				Create( Point1, Point2 );
				ShootEffects( select );
			}

			Stage = 0;
		}

		if ( Input.Pressed( "attack2" ) )
		{
			Stage = 0;
		}
	}

	[Rpc.Host]
	private void Create( SelectionPoint point1, SelectionPoint point2 )
	{
		CreateConstraint( point1, point2 );
	}

	protected abstract void CreateConstraint( SelectionPoint point1, SelectionPoint point2 );
}
