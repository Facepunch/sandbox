
namespace Sandbox.UI;

public class NoticePanel : Panel
{
	bool initialized;
	Vector3.SpringDamped _springy;

	public RealTimeUntil TimeUntilDie;

	public bool IsDead => TimeUntilDie < 0;

	internal void UpdatePosition( Vector2 vector2 )
	{
		if ( initialized == false )
		{
			_springy = new Vector3.SpringDamped( new Vector3( vector2.x + Random.Shared.Float( 300, 400 ), vector2.y + Random.Shared.Float( -10, 10 ), 0 ), 20.0f, 0.0f );
			_springy.Velocity = Vector3.Random * 1000;
			initialized = true;
		}

		if ( TimeUntilDie < -2 )
		{
			Delete();
			return;
		}

		if ( IsDead )
		{
			vector2.x += 500;
		}

		_springy.Target = new Vector3( vector2.x, vector2.y, 0 );
		_springy.Frequency = 4;
		_springy.Damping = 0.3f;
		_springy.SmoothTime = 2134120.1f;
		_springy.Update( RealTime.Delta * 1.0f );

		Style.Left = _springy.Current.x * ScaleFromScreen;
		Style.Top = _springy.Current.y * ScaleFromScreen;
	}
}
