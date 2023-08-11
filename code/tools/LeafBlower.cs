namespace Sandbox.Tools
{
	[Library( "tool_leafblower", Title = "Leaf Blower", Description = "Blow me", Group = "fun" )]
	public partial class LeafBlowerTool : BaseTool
	{
		protected virtual float Force => 128;
		protected virtual float MaxDistance => 512;
		protected virtual bool Massless => true;

		public override void Simulate()
		{
			if ( !Game.IsServer )
				return;

			using ( Prediction.Off() )
			{
				bool push = Input.Down( "attack1" );
				if ( !push && !Input.Down( "attack2" ) )
					return;

				var tr = DoTrace();

				if ( !tr.Hit )
					return;

				if ( !tr.Entity.IsValid() )
					return;

				if ( tr.Entity.IsWorld )
					return;

				var body = tr.Body;

				if ( !body.IsValid() )
					return;

				var direction = tr.EndPosition - tr.StartPosition;
				var distance = direction.Length;
				var ratio = (1.0f - (distance / MaxDistance)).Clamp( 0, 1 ) * (push ? 1.0f : -1.0f);
				var force = direction * (Force * ratio);

				if ( Massless )
				{
					force *= body.Mass;
				}

				body.ApplyForceAt( tr.EndPosition, force );
			}
		}
	}
}
