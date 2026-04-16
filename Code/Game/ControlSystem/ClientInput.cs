

using Sandbox.Utility;

public struct ClientInput
{
	readonly record struct State( Connection connection, PlayerController playerController );

	static State _currentState;

	static Connection Connection => _currentState.connection;

	public readonly bool IsEnabled => !string.IsNullOrWhiteSpace( Action );

	public string Action { get; set; }

	/// <summary>
	/// Returns an analog value between 0 and 1 representing how much the input is pressed
	/// </summary>
	public readonly float GetAnalog()
	{
		if ( !IsEnabled ) return 0;
		return Down() ? 1 : 0;
	}

	/// <summary>
	/// Returns true if button is currently held down
	/// </summary>
	public readonly bool Down()
	{
		if ( !IsEnabled ) return false;

		return Connection?.Down( Action ) ?? false;
	}

	/// <summary>
	/// Returns true if button was released
	/// </summary>
	public readonly bool Released()
	{
		if ( !IsEnabled ) return false;

		return Connection?.Released( Action ) ?? false;
	}

	/// <summary>
	/// Returns true if button was pressed
	/// </summary>
	public readonly bool Pressed()
	{
		if ( !IsEnabled ) return false;

		return Connection?.Pressed( Action ) ?? false;
	}

	internal static IDisposable PushScope( PlayerController player )
	{
		var previousState = _currentState;
		_currentState = new State( player?.Network?.Owner, player );

		return DisposeAction.Create( () => _currentState = previousState );
	}

	/// <summary>
	/// The player currently running an <see cref="IPlayerControllable.OnControl"/> tick,
	/// or null when not inside a control scope (e.g. during regular player input).
	/// </summary>
	public static PlayerController Current => _currentState.playerController;
}
