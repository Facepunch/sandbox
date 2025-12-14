
[Hide]
public class Thruster : Component
{
	[Range( 0, 1 )]
	public float Power { get; set; } = 0.5f;

	/// <summary>
	/// While the client input is active we'll apply thrust
	/// </summary>
	[Property, Sync, ClientEditable]
	public ClientInput Activate { get; set; }

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		AddThrust( Activate.GetAnalog() );
	}

	void AddThrust( float amount )
	{
		if ( amount.AlmostEqual( 0.0f ) ) return;

		var body = GetComponent<Rigidbody>();
		if ( body == null ) return;

		body.ApplyImpulse( WorldRotation.Up * -10000 * amount * Power );
	}
}


public struct ClientInput
{
	static Connection Current => Connection.Local;

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

		return Current?.Down( Action ) ?? false;
	}

	/// <summary>
	/// Returns true if button was released
	/// </summary>
	public readonly bool Released()
	{
		if ( !IsEnabled ) return false;

		return Current?.Released( Action ) ?? false;
	}

	/// <summary>
	/// Returns true if button was pressed
	/// </summary>
	public readonly bool Pressed()
	{
		if ( !IsEnabled ) return false;

		return Current?.Pressed( Action ) ?? false;
	}
}
