using Sandbox.Npcs.Layers;

namespace Sandbox.Npcs;

public partial class Npc : Component
{
	readonly List<BaseNpcLayer> _layers = [];

	/// <summary>
	/// All layers in this npc
	/// </summary>
	public IReadOnlyList<BaseNpcLayer> Layers => _layers;

	/// <summary>
	/// Senses layer - handles environmental awareness and target detection
	/// </summary>
	public SensesLayer Senses { get; }

	/// <summary>
	/// Navigation layer - handles pathfinding and movement
	/// </summary>
	public NavigationLayer Navigation { get; }

	/// <summary>
	/// Animation layer - handles look-at and animation parameters
	/// </summary>
	public AnimationLayer Animation { get; }

	/// <summary>
	/// Speech layer - handles talking, lipsync, etc..
	/// </summary>
	public SpeechLayer Speech { get; }

	/// <summary>
	/// Creates, initializes, and adds a new layer of the specified type to the collection of layers. Generally called
	/// in the constructor of the NPC.
	/// </summary>
	protected T AddLayer<T>() where T : BaseNpcLayer, new()
	{
		var layer = new T();
		layer.Initialize( this );
		_layers.Add( layer );
		return layer;
	}
}
