using System;

namespace Sandbox.Tools
{
	[Library( "tool_resizer", Title = "Resizer", Description = "Change the scale of things", Group = "construction" )]
	public partial class ResizerTool : BaseTool
	{
		public override void Simulate()
		{
			if ( !Game.IsServer )
				return;

			using ( Prediction.Off() )
			{
				var startPos = Owner.EyePosition;
				var dir = Owner.EyeRotation.Forward;
				int resizeDir = 0;
				var reset = false;

				if ( Input.Down( "attack1" ) ) resizeDir = 1;
				else if ( Input.Down( "attack2" ) ) resizeDir = -1;
				else if ( Input.Pressed( "reload" ) ) reset = true;
				else return;

				var tr = DoTrace();

				if ( !tr.Hit || !tr.Entity.IsValid() )
					return;

				var entity = tr.Entity.Root;
				if ( !entity.IsValid() )
					return;

				if ( entity.PhysicsGroup == null )
					return;

				var scale = reset ? 1.0f : Math.Clamp( entity.Scale + ((0.5f * Time.Delta) * resizeDir), 0.4f, 4.0f );

				if ( entity.Scale != scale )
				{
					entity.Scale = scale;
					entity.PhysicsGroup.RebuildMass();
					entity.PhysicsGroup.Sleeping = false;

					foreach ( var child in entity.Children )
					{
						if ( !child.IsValid() )
							continue;

						if ( child.PhysicsGroup == null )
							continue;

						child.PhysicsGroup.RebuildMass();
						child.PhysicsGroup.Sleeping = false;
					}
				}

				CreateHitEffects( tr.EndPosition, tr.Normal, true );
			}
		}
	}
}
