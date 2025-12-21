namespace Sandbox.Npcs.Layers;

/// <summary>
/// A behavior layer provides specific services for tasks to use -- we don't use behavior layers for state, they are services.
/// </summary>
public abstract class BaseNpcLayer
{
	protected Npc Npc { get; private set; }

	public BaseNpcLayer()
	{
	}

	internal void Initialize( Npc npc )
	{
		Npc = npc;
	}

	internal void InternalOnStart()
	{
		OnStart();
	}

	/// <summary>
	/// Called when the layer is created
	/// </summary>
	protected virtual void OnStart() { }


	internal void InternalUpdate()
	{
		OnUpdate();
	}

	/// <summary>
	/// Called each frame to update the layer
	/// </summary>
	protected virtual void OnUpdate() { }

	/// <summary>
	/// Reset the layer state
	/// </summary>
	public virtual void Reset() { }
}
