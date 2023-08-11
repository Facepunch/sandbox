namespace Sandbox.Tools
{
	[Library( "tool_remover", Title = "Remover", Description = "Remove entities", Group = "construction" )]
	public partial class RemoverTool : BaseTool
	{
		public override void Simulate()
		{
			if ( !Game.IsServer )
				return;

			using ( Prediction.Off() )
			{
				if ( !Input.Pressed( "attack1" ) )
					return;

				var tr = DoTrace();

				if ( !tr.Hit || !tr.Entity.IsValid() )
					return;

				if ( tr.Entity is Player )
					return;

				CreateHitEffects( tr.EndPosition, tr.Normal );

				if ( tr.Entity.IsWorld )
					return;

				tr.Entity.Delete();

				var particle = Particles.Create( "particles/physgun_freeze.vpcf" );
				particle.SetPosition( 0, tr.Entity.Position );
			}
		}
	}
}
