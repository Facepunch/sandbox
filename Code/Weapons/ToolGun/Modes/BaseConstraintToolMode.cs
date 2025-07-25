
using Sandbox.Rendering;

public abstract class BaseConstraintToolMode : ToolMode
{
	protected SelectionPoint Point1;
	protected SelectionPoint Point2;
	protected int Stage = 0;

	public virtual bool CanConstraintToSelf => false;

	public bool IsValidState { get; protected set; }

	public override void OnControl()
	{
		base.OnControl();

		if ( Input.Down( "attack2" ) )
		{
			Stage = 0;
			IsValidState = false;
			return;
		}

		IsValidState = true;

		var select = TraceSelect();

		if ( !select.IsValid() )
			return;

		if ( Stage == 0 )
		{
			IsValidState = UpdateValidity( select );
		}

		if ( Stage == 1 )
		{
			IsValidState = UpdateValidity( Point1, select );
		}

		if ( !IsValidState ) return;

		if ( Input.Pressed( "attack1" ) )
		{
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


	}

	bool UpdateValidity( SelectionPoint point1, SelectionPoint point2 )
	{
		if ( !point1.GameObject.IsValid() ) return false;
		if ( !point2.GameObject.IsValid() ) return false;

		if ( !CanConstraintToSelf )
		{
			if ( point1.GameObject == point2.GameObject )
			{
				return false;
			}
		}

		return true;
	}

	bool UpdateValidity( SelectionPoint point1 )
	{
		if ( !point1.GameObject.IsValid() ) return false;

		return true;
	}

	[Rpc.Host]
	private void Create( SelectionPoint point1, SelectionPoint point2 )
	{
		CreateConstraint( point1, point2 );
	}

	protected abstract void CreateConstraint( SelectionPoint point1, SelectionPoint point2 );

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		if ( IsValidState )
		{
			painter.SetBlendMode( BlendMode.Normal );
			painter.DrawCircle( crosshair, 5, Color.Black );
			painter.DrawCircle( crosshair, 3, Color.White );
		}
		else
		{
			Color redColor = "#e53";
			painter.SetBlendMode( BlendMode.Normal );
			painter.DrawCircle( crosshair, 5, redColor.Darken( 0.3f ) );
			painter.DrawCircle( crosshair, 3, redColor );
		}
	}
}
