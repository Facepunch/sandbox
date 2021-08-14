namespace Sandbox.Tools
{
	[Library( "tool_debug", Title = "Debug Tool", Description = "", Group = "construction" )]
	public partial class DebugTool : BaseTool
	{
		public override void Simulate()
		{
			if ( !Host.IsServer )
				return;

			using ( Prediction.Off() )
			{
				if ( Input.Pressed( InputButton.Attack1 ) )
				{
					for ( int i = 0; i < Entity.All.Count; i++ )
					{
						Entity e = Entity.All[i];
						Log.Info($"Entity: {e.GetType()} | Position: {e.Position} |Owner: {e.Owner} |Client: {e.GetClientOwner()?.Name}");

						if ( e is ToolCameraEntity tc )
						{
							Log.Info( $"Found ToolCameraEntity: {tc.GetHashCode()}, phys = {tc.PhysicsEnabled}" );
						}
					}
				}

				if ( Input.Pressed (InputButton.Attack2) )
				{
					for ( int i = 0; i < Entity.All.Count; i++ )
					{
						Entity e = Entity.All[i];
						if ( e is ToolCameraEntity tc )
						{
							tc.Delete();
						}
					}
				}

				if ( Input.Pressed (InputButton.Reload) )
				{
					Log.Info($"Screen: H:{Screen.Height} W:{Screen.Width} Size: {Screen.Size} Aspect: {Screen.Aspect}");
				}
			}
		}

		public override void Activate()
		{
			base.Activate();
		}

		public override void Deactivate()
		{
			base.Deactivate();
		}
	}
}
