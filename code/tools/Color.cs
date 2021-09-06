using System;

namespace Sandbox.Tools
{
	[Library( "tool_color", Title = "Color", Description = "Change render color and alpha of entities", Group = "construction" )]
	public partial class ColorTool : BaseTool
	{
		public override void Simulate()
		{
			if ( !Host.IsServer )
				return;

			using ( Prediction.Off() )
			{
				var startPos = Owner.EyePos;
				var dir = Owner.EyeRot.Forward;

				if ( !Input.Pressed( InputButton.Attack1 ) ) return;

				var tr = Trace.Ray( startPos, startPos + dir * MaxTraceDistance )
				   .Ignore( Owner )
				   .UseHitboxes()
				   .HitLayer( CollisionLayer.Debris )
				   .Run();

				if ( !tr.Hit || !tr.Entity.IsValid() )
					return;

				if ( tr.Entity is not ModelEntity modelEnt )
					return;


				Color32 pastColor = modelEnt.RenderColor;
				modelEnt.RenderColor = Color.Random.ToColor32();

				if ( tr.Entity is not WorldEntity )
					Undo.AddItem( new UndoEntry( Owner, new ModelColorUndo( modelEnt, pastColor ), "Color" ) );

				CreateHitEffects( tr.EndPos );
			}
		}
	}
}
