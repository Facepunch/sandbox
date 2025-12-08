namespace Sandbox;

/// <summary>
/// Indicates that this propery can be edited by the client, in a game like Sandbox Mode.
/// </summary>
[AttributeUsage( AttributeTargets.Property )]
public sealed class ClientEditableAttribute : Attribute
{
}
