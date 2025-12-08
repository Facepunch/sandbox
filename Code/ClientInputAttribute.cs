namespace Sandbox;

/// <summary>
/// Indicates that this method can be controlled by the client, in a game like Sandbox Mode.
/// </summary>
[AttributeUsage( AttributeTargets.Method )]
public sealed class ClientInputAttribute : Attribute
{
}
