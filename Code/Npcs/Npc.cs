using Sandbox.Npcs.Layers;

namespace Sandbox.Npcs;

[Hide]
public partial class Npc : Component
{
	[Property]
	public bool ShowDebugOverlay { get; set; }

	public Npc()
	{
		Senses = AddLayer<SensesLayer>();
		Navigation = AddLayer<NavigationLayer>();
		Animation = AddLayer<AnimationLayer>();
		Speech = AddLayer<SpeechLayer>();
	}

	protected override void OnStart()
	{
		base.OnStart();

		foreach ( var layer in _layers )
		{
			layer.InternalOnStart();
		}
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		foreach ( var layer in _layers )
		{
			layer.InternalUpdate();
		}

		TickSchedule();

		if ( ShowDebugOverlay )
		{
			DrawDebugString();
		}
	}
}
