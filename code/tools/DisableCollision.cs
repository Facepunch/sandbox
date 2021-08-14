namespace Sandbox.Tools
{
	[Library( "tool_disablecollision", Title = "Disable Collision", Description = "Disable Collisions on props\nDoes not work on Entities!\nAttack 1 Disable Collisions\nAttack 2 Enable Collisions", Group = "construction" )]
	public partial class DisableCollision: BaseTool
	{
		public override void Simulate()
		{
			if ( !Host.IsServer )
				return;

			using ( Prediction.Off() )
			{
				if ( Input.Pressed( InputButton.Attack1 ) )
				{
					var startPos = Owner.EyePos;
					var dir = Owner.EyeRot.Forward;

					var tr = Trace.Ray( startPos, startPos + dir * MaxTraceDistance )
						.Ignore( Owner )
						.Run();

					if ( !tr.Hit )
						return;

					if ( tr.Entity.IsWorld )
						return;

					if ( !tr.Entity.IsValid() )
						return;

					if ( !(tr.Body.IsValid()) )
						return;

					if ( tr.Entity is ModelEntity model )
					{
						model.CollisionGroup = CollisionGroup.Debris;
						CreateHitEffects( tr.EndPos );
					}
				}

				if ( Input.Pressed( InputButton.Attack2 ) )
				{
					var startPos = Owner.EyePos;
					var dir = Owner.EyeRot.Forward;

					var tr = Trace.Ray( startPos, startPos + dir * MaxTraceDistance )
						.Ignore( Owner )
						.Run();

					if ( !tr.Hit )
						return;

					if ( tr.Entity.IsWorld )
						return;

					if ( !tr.Entity.IsValid() )
						return;

					if ( !(tr.Body.IsValid()) )
						return;

					if ( !(tr.Entity is ModelEntity) || (tr.Entity as ModelEntity).CollisionGroup == CollisionGroup.Interactive )
					{
						return;
					}

					(tr.Entity as ModelEntity).CollisionGroup = CollisionGroup.Interactive;

					CreateHitEffects( tr.EndPos );
				}
			}
		}
	}
}
