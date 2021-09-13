using System;

namespace Sandbox.Tools
{
	[Library( "tool_resizer", Title = "Resizer", Description = "Change the scale of things", Group = "construction" )]
	public partial class ResizerTool : BaseTool
	{
		public override void Simulate()
		{
			if ( !Host.IsServer )
				return;

			using ( Prediction.Off() )
			{
				var startPos = Owner.EyePos;
				var dir = Owner.EyeRot.Forward;
				int resizeDir = 0;
				var reset = false;

				if ( Input.Down( InputButton.Attack1 ) ) resizeDir = 1;
				else if ( Input.Down( InputButton.Attack2 ) ) resizeDir = -1;
				else if ( Input.Pressed( InputButton.Reload ) ) reset = true;
				else return;

				var tr = Trace.Ray( startPos, startPos + dir * MaxTraceDistance )
				   .Ignore( Owner )
				   .UseHitboxes()
				   .HitLayer( CollisionLayer.Debris )
				   .Run();

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
					entity.PhysicsGroup.Wake();

					foreach ( var child in entity.Children )
					{
						if ( !child.IsValid() )
							continue;

						if ( child.PhysicsGroup == null )
							continue;

						child.PhysicsGroup.RebuildMass();
						child.PhysicsGroup.Wake();
					}
				}

				if ( Input.Pressed( InputButton.Attack1 ) || Input.Pressed( InputButton.Attack2 ) || reset )
				{
					CreateHitEffects( tr.EndPos );
				}
			}
		}
	}
}
