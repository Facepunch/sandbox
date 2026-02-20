
namespace Sandbox.UI;

public class NoticePanel : Panel
{
	bool initialized;
	Vector3.SpringDamped _springy;

	public RealTimeUntil TimeUntilDie;

	/// <summary>
	/// If true, the notice won't auto-dismiss. Call <see cref="Dismiss"/> to remove it.
	/// </summary>
	public bool Manual { get; set; }

	public bool IsDead => !Manual && TimeUntilDie < 0;
	public bool wasDead = false;

	/// <summary>
	/// Dismiss a manual notice, causing it to slide out and be deleted.
	/// </summary>
	public void Dismiss()
	{
		Manual = false;
		TimeUntilDie = 0;
	}

	internal void UpdatePosition( Vector2 vector2 )
	{
		if ( initialized == false )
		{
			_springy = new Vector3.SpringDamped( new Vector3( Screen.Width + 50, vector2.y + Random.Shared.Float( -10, 10 ), 0 ), 0.0f );
			_springy.Velocity = Vector3.Random * 1000;
			initialized = true;
		}

		if ( !Manual && TimeUntilDie < 0.4f )
		{
			vector2.x -= 50;
		}

		// we're dead, push us out to rhe right
		if ( IsDead )
		{
			vector2.x = Screen.Width + 50;

			// we've been dead for 2 seconds, get rid of us
			if ( TimeUntilDie < -2 )
			{
				Delete();
				return;
			}

			wasDead = true;
		}

		_springy.Target = new Vector3( vector2.x, vector2.y, 0 );
		_springy.Frequency = 4;
		_springy.Damping = 0.5f;
		_springy.Update( RealTime.Delta * 1.0f );

		Style.Left = _springy.Current.x * ScaleFromScreen;
		Style.Top = _springy.Current.y * ScaleFromScreen;
	}
}
