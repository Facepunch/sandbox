

using Sandbox.Utility;

public class Thruster : Component, IPlayerControllable
{
	[Property, Range( 0, 1 )]
	public GameObject OnEffect { get; set; }

	[Property, Range( 0, 1 )]
	public float Power { get; set; } = 0.5f;

	/// <summary>
	/// While the client input is active we'll apply thrust
	/// </summary>
	[Property, Sync, ClientEditable]
	public ClientInput Activate { get; set; }

	protected override void OnEnabled()
	{
		base.OnEnabled();

		OnEffect?.Enabled = false;
	}

	void AddThrust( float amount )
	{
		if ( amount.AlmostEqual( 0.0f ) ) return;

		var body = GetComponent<Rigidbody>();
		if ( body == null ) return;

		body.ApplyImpulse( WorldRotation.Up * -10000 * amount * Power );
	}

	bool _state;

	public void SetActiveState( bool state )
	{
		if ( _state == state ) return;

		_state = state;

		OnEffect?.Enabled = state;

		Network.Refresh();

	}

	public void OnStartControl()
	{
	}

	public void OnEndControl()
	{
	}

	public void OnControl()
	{
		var analog = Activate.GetAnalog();

		AddThrust( analog );
		SetActiveState( analog > 0.1f );
	}
}


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
}
