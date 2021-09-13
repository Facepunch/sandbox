using System;
using Sandbox;

namespace Sandbox.Tools
{
	[Library( "tool_toolcamera", Title = "Camera", Description = "Spawn a Camera\nAttack 1 for a non-physical Camera\nAttack 2 for a physics enabled Camera\nReload to toggle the camera", Group = "render" )]
	public partial class CameraTool : BaseTool
	{
		public override void Simulate()
		{
			if ( !Host.IsServer )
				return;

			using ( Prediction.Off() )
			{
				if ( Input.Pressed (InputButton.Attack1) )
				{
					createCamera(false);
				}
				else if ( Input.Pressed (InputButton.Attack2) )
				{
					createCamera(true);
				}
				else if (Input.Pressed (InputButton.Reload))
				{
					toggleCam();
				}
			}
		}

		private void createCamera(bool enablePhys)
		{
			//Check if a camera already exists
			//As we don't have the ability to make custom enable/disable keys
			//we can only support one camera at a time
			for ( int i = 0; i < Entity.All.Count; i++ )
			{
				//Don't delete other peoples cameras
				if ( Entity.All[i] is ToolCameraEntity tc && tc.Owner == this.Owner)
				{
					tc.Delete();
				}
			}

			var ent = new ToolCameraEntity
			{
				Position = Owner.EyePos,
				Rotation = Owner.EyeRot,
				Owner = this.Owner
			};

			ent.SetPhys( enablePhys );

			CreateHitEffects( ent.Position );
		}

		private void toggleCam()
		{
			if ( !FoundCamEnt() )
			{
				return;
			}

			if ( Owner is SandboxPlayer player)
			{
				if ( player.MainCamera is TestCamera )
				{
					player.MainCamera = new FirstPersonCamera();
				}
				else
				{
					player.MainCamera = new TestCamera();
				}
			}
		}

		private bool FoundCamEnt()
		{
			//There has to be a better way of checking if the player has spawned an entity
			//since we don't support multiple cameras we return true on the first one that matches the owner
			//TODO: Check out the new tags
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
