namespace Sandbox.Tools
{
	[Library( "tool_rope", Title = "Rope", Description = "Join two things together with a rope", Group = "construction" )]
	public partial class RopeTool : BaseTool
	{
		private PhysicsBody targetBody;
		private int targetBone;
		private Vector3 localOrigin1;
		private Vector3 globalOrigin1;

		public override void Simulate()
		{
			if ( !Host.IsServer )
				return;

			using ( Prediction.Off() )
			{
				if ( !Input.Pressed( InputButton.Attack1 ) )
					return;

				var startPos = Owner.EyePos;
				var dir = Owner.EyeRot.Forward;

				var tr = Trace.Ray( startPos, startPos + dir * MaxTraceDistance )
					.Ignore( Owner )
					.Run();

				if ( !tr.Hit )
					return;

				if ( !tr.Body.IsValid() )
					return;

				if ( !tr.Entity.IsValid() )
					return;

				if ( tr.Entity is not ModelEntity )
					return;

				if ( !targetBody.IsValid() )
				{
					targetBody = tr.Body;
					targetBone = tr.Bone;
					globalOrigin1 = tr.EndPos;
					localOrigin1 = tr.Body.Transform.PointToLocal( globalOrigin1 );

					CreateHitEffects( tr.EndPos );

					return;
				}

				if ( targetBody == tr.Body )
					return;

				var rope = Particles.Create( "particles/rope.vpcf" );

				if ( targetBody.Entity.IsWorld )
				{
					rope.SetPosition( 0, localOrigin1 );
				}
				else
				{
					rope.SetEntityBone( 0, targetBody.Entity, targetBone, new Transform( localOrigin1 * (1.0f / targetBody.Entity.Scale) ) );
				}

				var localOrigin2 = tr.Body.Transform.PointToLocal( tr.EndPos );

				if ( tr.Entity.IsWorld )
				{
					rope.SetPosition( 1, localOrigin2 );
				}
				else
				{
					rope.SetEntityBone( 1, tr.Body.Entity, tr.Bone, new Transform( localOrigin2 * (1.0f / tr.Entity.Scale) ) );
				}

				var spring = PhysicsJoint.Spring
					.From( targetBody, localOrigin1 )
					.To( tr.Body, localOrigin2 )
					.WithFrequency( 5.0f )
					.WithDampingRatio( 0.7f )
					.WithReferenceMass( targetBody.Mass )
					.WithMinRestLength( 0 )
					.WithMaxRestLength( tr.EndPos.Distance( globalOrigin1 ) )
					.WithCollisionsEnabled()
					.Create();

				spring.EnableAngularConstraint = false;
				spring.OnBreak( () =>
				{
					rope?.Destroy( true );
					spring.Remove();
				} );

				CreateHitEffects( tr.EndPos );

				Reset();
			}
		}

		private void Reset()
		{
			targetBody = null;
			targetBone = -1;
			localOrigin1 = default;
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
