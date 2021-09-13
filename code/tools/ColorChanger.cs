using System;

namespace Sandbox.Tools
{
	[Library( "tool_colorchanger", Title = "Color Changer", Description = "Change color of objects using Mouse 1 or 2", Group = "construction" )]
	public partial class ColorChanger : BaseTool
	{

		private Color[] colors =
		{	Color.Black,
			Color.Blue,
			Color.Cyan,
			Color.Gray,
			Color.Green,
			Color.Magenta,
			Color.Orange,
			Color.Red,
			Color.Transparent,
			Color.White,
			Color.Yellow,
		};

		private int colorIndex = 0;

		public override void Simulate()
		{
			if ( !Host.IsServer )
				return;

			using ( Prediction.Off() )
			{
				var startPos = Owner.EyePos;
				var dir = Owner.EyeRot.Forward;

				if ( Input.Pressed( InputButton.Attack1 ) )
				{
					colorIndex++;

					if ( colorIndex > colors.Length - 1 )
					{
						colorIndex = 0;
					}

					//Log.Info( "colorIndex went up: " + colorIndex );
				}
				else if ( Input.Pressed( InputButton.Attack2 ) )
				{
					colorIndex--;

					if ( colorIndex < 0 )
					{
						colorIndex = colors.Length - 1;
					}

					//Log.Info( "colorIndex went down: " + colorIndex );
				}
				else return;

				var tr = Trace.Ray( startPos, startPos + dir * MaxTraceDistance )
				   .Ignore( Owner )
				   .UseHitboxes()
				   .HitLayer( CollisionLayer.Debris )
				   .Run();

				if ( !tr.Hit || !tr.Entity.IsValid() )
					return;

				if ( tr.Entity is not ModelEntity modelEnt )
					return;

				try
				{
					//modelEnt.RenderColorAndAlpha = getColor( colorIndex );
					modelEnt.RenderColor = getColor(colorIndex);
					
					if (colorIndex == 8)
					{
						modelEnt.RenderAlpha = 0;
					}
					else
					{
						modelEnt.RenderAlpha = 100;
					}
				}
				catch ( IndexOutOfRangeException ex )
				{
					Log.Error(ex, "colorIndex out of range: " + colorIndex);
					colorIndex = 0;
				}

				CreateHitEffects( tr.EndPos ); 
			}
		}

		private Color getColor(int index)
		{
			return colors[index];
		}
	}
}
