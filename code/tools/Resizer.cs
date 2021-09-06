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

				if ( !tr.Hit || !tr.Entity.IsValid() || tr.Entity.PhysicsGroup == null )
					return;

				// Disable resizing lights for now
				if ( tr.Entity is LightEntity || tr.Entity is LampEntity )
					return;

				float postScale = tr.Entity.Scale;
				var scale = reset ? 1.0f : Math.Clamp( tr.Entity.Scale + ((0.5f * Time.Delta) * resizeDir), 0.4f, 4.0f );

				if ( tr.Entity.Scale != scale )
				{
					tr.Entity.Scale = scale;
					tr.Entity.PhysicsGroup.RebuildMass();
					tr.Entity.PhysicsGroup.Wake();

					foreach ( var child in tr.Entity.Children )
					{
						if ( !child.IsValid() )
							continue;

						child.PhysicsGroup.RebuildMass();
						child.PhysicsGroup.Wake();
					}
				}

				if ( Input.Pressed( InputButton.Attack1 ) || Input.Pressed( InputButton.Attack2 ) || reset )
				{
					Undo.AddItem( new UndoEntry( Owner, new EntityScaleUndo( tr.Entity, postScale ), "Resizer" ) );
					CreateHitEffects( tr.EndPos );
				}
			}
		}
	}
}
