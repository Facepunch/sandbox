using System;
using Sandbox;

namespace Sandbox.Tools
{
	[Library( "tool_toolcamera", Title = "Camera Spawner", Description = "Spawn a Camera\nAttack 1 for a non-physical Camera\nAttack 2 for a physics enabled Camera", Group = "render" )]
	public partial class CameraToolSpawner : BaseTool
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

				if ( Input.Pressed (InputButton.Attack2) )
				{
					createCamera(true);
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
				if ( Entity.All[i] is ToolCameraEntity tc && Entity.All[i].Owner == this.Owner)
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
	}
}
