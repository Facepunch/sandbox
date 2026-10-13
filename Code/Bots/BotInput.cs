namespace Sandbox.Bots;

/// <summary>
/// The buttons and sticks a bot brain holds. Feeds a bot, or the local player when there's no bot.
/// </summary>
public sealed class BotInput
{
	readonly Bot _bot;
	readonly HashSet<string> _down = new( StringComparer.OrdinalIgnoreCase );
	// Real time, taps are set from the brain loop and expired in Frame
	readonly Dictionary<string, RealTimeUntil> _taps = new( StringComparer.OrdinalIgnoreCase );

	// What the local player's frame and fixed updates last saw, so Pressed and Released work
	readonly HashSet<string> _lastFrame = new( StringComparer.OrdinalIgnoreCase );
	readonly HashSet<string> _lastFixed = new( StringComparer.OrdinalIgnoreCase );
	readonly List<string> _scratch = new();

	Angles _look;
	Vector2 _wheel;

	public BotInput( Bot bot )
	{
		_bot = bot;
	}

	public Vector3 AnalogMove { get; set; }

	public bool IsDown( string action ) => _down.Contains( action );

	public void SetAction( string action, bool down )
	{
		_taps.Remove( action );
		if ( down ? !_down.Add( action ) : !_down.Remove( action ) )
			return;

		_bot?.SetAction( action, down );
	}

	/// <summary>
	/// Press briefly, long enough for a few fixed updates to see it.
	/// </summary>
	public void Tap( string action )
	{
		SetAction( action, true );
		_taps[action] = 0.1f;
	}

	public void ReleaseAll()
	{
		foreach ( var action in _down.ToArray() )
			SetAction( action, false );
	}

	public void Look( Angles delta ) => _look = delta;

	public void AddMouseWheel( Vector2 delta )
	{
		if ( _bot is not null ) _bot.AddMouseWheel( delta );
		else _wheel += delta;
	}

	/// <summary>
	/// Once a frame, before anything reads input.
	/// </summary>
	public void Frame()
	{
		foreach ( var (action, until) in _taps )
		{
			if ( until <= 0 ) _scratch.Add( action );
		}

		foreach ( var action in _scratch )
			SetAction( action, false );

		_scratch.Clear();

		if ( _bot is not null )
		{
			_bot.AnalogMove = AnalogMove;
			_bot.AnalogLook = _look;
			return;
		}

		Input.AnalogMove = AnalogMove;
		Input.AnalogLook = _look;
		Input.MouseWheel += _wheel;
		_wheel = default;
		Apply( _lastFrame );
	}

	/// <summary>
	/// At the start of each fixed update. Only the local player needs it, bots have their own fixed input.
	/// </summary>
	public void FixedUpdate()
	{
		if ( _bot is null )
			Apply( _lastFixed );
	}

	// The local player's input is the real devices, so overwrite this update's actions with ours
	void Apply( HashSet<string> last )
	{
		foreach ( var action in last )
		{
			Input.SetLastAction( action, true );
			if ( !_down.Contains( action ) ) Input.SetAction( action, false );
		}

		foreach ( var action in _down )
		{
			Input.SetAction( action, true );
			if ( !last.Contains( action ) ) Input.SetLastAction( action, false );
		}

		last.Clear();
		last.UnionWith( _down );
	}
}
