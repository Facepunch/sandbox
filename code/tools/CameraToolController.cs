using System;
using Sandbox;

namespace Sandbox.Tools
{
	[Library( "tool_toolcameracontroller", Title = "Camera Controller", Description = "Attack 1: Enter Camera\nAttack 2: Leave Camera\nMake sure to spawn a camera first!", Group = "render" )]
	public partial class CameraToolController: BaseTool
	{
		public override void Simulate()
		{
			if ( !Host.IsServer )
				return;

			using ( Prediction.Off() )
			{

				if ( !FoundCamEnt() )
				{
					DebugOverlay.ScreenText( new Vector2( Screen.Width / 2, Screen.Height / 2 ), 0, Color.Yellow, "Use Camera Spawner first!", 1 );
					return;
				}

				if ( Input.Pressed (InputButton.Attack1) )
				{
					if ( Owner is SandboxPlayer player )
					{
						if ( player.MainCamera is TestCamera )
						{
							return;
						}

						player.MainCamera = new TestCamera();
						CreateHitEffects( ((this.Owner as SandboxPlayer).MainCamera as TestCamera).Pos );
					}
				}

				if ( Input.Pressed( InputButton.Attack2 ) )
				{
					if ( Owner is SandboxPlayer player )
					{
						if ( player.MainCamera is FirstPersonCamera ||player.MainCamera is ThirdPersonCamera )
						{
							return;
						}
						player.MainCamera = new FirstPersonCamera();
						CreateHitEffects( this.Owner.Position );
					}
				}
			}
		}

		private bool FoundCamEnt()
		{
			//There has to be a better way of checking if the player has spawned an entity
			//since we don't support multiple cameras we return the first one that matches the owner
			for ( int i = 0; i < Entity.All.Count; i++ )
			{
				if ( Entity.All[i] is ToolCameraEntity entity && entity.Owner == this.Owner )
				{
					return true;
				}
			}

			return false;
		}
	}
}
