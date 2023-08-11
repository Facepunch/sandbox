using System;

namespace Sandbox.Tools
{
	[Library( "model_skin", Title = "Model Skin Changer", Description = "Cycles through the models skins", Group = "construction" )]
	public partial class ModelSkinTool : BaseTool
	{
		public override void Simulate()
		{
			if ( !Game.IsServer )
				return;

			using ( Prediction.Off() )
			{
				var startPos = Owner.EyePosition;
				var dir = Owner.EyeRotation.Forward;

				if ( !Input.Pressed( "attack1" ) ) return;

				var tr = DoTrace();

				if ( !tr.Hit || !tr.Entity.IsValid() )
					return;

				if ( tr.Entity is not ModelEntity modelEnt )
					return;

				if ( modelEnt.MaterialGroupCount == 0 )
				{
					return;
				}
				else
				{
					var currentGroup = modelEnt.GetMaterialGroup();
					var nextGroup = currentGroup + 1;

					if ( nextGroup >= modelEnt.MaterialGroupCount )
					{
						nextGroup = 0;
					}

					modelEnt.SetMaterialGroup( nextGroup );
				}

				CreateHitEffects( tr.EndPosition );
			}
		}
	}
}
