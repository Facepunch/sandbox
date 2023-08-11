namespace Sandbox.Tools
{
	[Library( "tool_weld", Title = "Parent", Description = "Parent stuff together", Group = "construction" )]
	public partial class WeldTool : BaseTool
	{
		private Prop target;

		public override void Simulate()
		{
			if ( !Game.IsServer )
				return;

			using ( Prediction.Off() )
			{
				var tr = DoTrace();

				if ( !tr.Hit || !tr.Body.IsValid() || !tr.Entity.IsValid() || tr.Entity.IsWorld )
					return;

				if ( tr.Entity.PhysicsGroup == null || tr.Entity.PhysicsGroup.BodyCount > 1 )
					return;

				if ( tr.Entity is not Prop prop )
					return;

				if ( Input.Pressed( "attack1" ) )
				{
					if ( prop.Root is not Prop rootProp )
					{
						return;
					}

					if ( target == rootProp )
						return;

					if ( !target.IsValid() )
					{
						target = rootProp;
					}
					else
					{
						rootProp.Weld( target );
						target = null;
					}
				}
				else if ( Input.Pressed( "attack2" ) )
				{
					prop.Unweld( true );

					Reset();
				}
				else if ( Input.Pressed( "reload" ) )
				{
					if ( prop.Root is not Prop rootProp )
					{
						return;
					}

					rootProp.Unweld();

					Reset();
				}
				else
				{
					return;
				}

				CreateHitEffects( tr.EndPosition, tr.Normal );
			}
		}

		private void Reset()
		{
			target = null;
		}

		public override void Activate()
		{
			base.Activate();

			Reset();
		}

		public override void Deactivate()
		{
			base.Deactivate();

			Reset();
		}
	}
}
