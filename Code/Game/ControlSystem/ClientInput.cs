using System.Text.Json.Serialization;

/// <summary>
/// A configurable input action for contraptions driven from a seat. This contains no state:
/// it reads the active driver's connection while <see cref="IPlayerControllable.OnControl"/>
/// is running. Wired inputs are ordinary <see cref="SignalInputAttribute"/> handlers instead.
/// </summary>
public struct ClientInput
{
	[Property]
	public string Action { get; set; }

	[JsonIgnore, Hide]
	public readonly bool IsEnabled => !string.IsNullOrWhiteSpace( Action );

	public readonly bool Down() => IsEnabled && ControlContext.Connection?.Down( Action ) == true;
	public readonly bool Pressed() => IsEnabled && ControlContext.Connection?.Pressed( Action ) == true;
	public readonly bool Released() => IsEnabled && ControlContext.Connection?.Released( Action ) == true;
	public readonly float GetAnalog() => Down() ? 1f : 0f;
}
