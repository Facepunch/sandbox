namespace Sandbox.Tools
{
	[Library( "tool_light", Title = "Lights", Description = "A dynamic point light", Group = "construction" )]
	public partial class LightTool : BaseTool
	{
		PreviewEntity previewModel;

		private string Model => "models/light/light_tubular.vmdl";

		protected override bool IsPreviewTraceValid( TraceResult tr )
		{
			if ( !base.IsPreviewTraceValid( tr ) )
				return false;

			if ( tr.Entity is LightEntity )
				return false;

			return true;
		}

		public override void CreatePreviews()
		{
			if ( TryCreatePreview( ref previewModel, Model ) )
			{
				previewModel.RelativeToNormal = false;
				previewModel.OffsetBounds = true;
				previewModel.PositionOffset = -previewModel.CollisionBounds.Center;
			}
		}

		public override void Simulate()
		{
			if ( !Host.IsServer )
				return;

			using ( Prediction.Off() )
			{
				bool useRope = Input.Pressed( InputButton.Attack1 );
				if ( !useRope && !Input.Pressed( InputButton.Attack2 ) )
					return;

				var startPos = Owner.EyePos;
				var dir = Owner.EyeRot.Forward;

				var tr = Trace.Ray( startPos, startPos + dir * MaxTraceDistance )
					.Ignore( Owner )
					.Run();

				if ( !tr.Hit || !tr.Entity.IsValid() )
					return;

				CreateHitEffects( tr.EndPos );

				if ( tr.Entity is LightEntity )
				{
					// TODO: Set properties

					return;
				}

				var light = new LightEntity
				{
					Enabled = true,
					DynamicShadows = false,
					Range = 128,
					Falloff = 1.0f,
					LinearAttenuation = 0.0f,
					QuadraticAttenuation = 1.0f,
					Brightness = 1,
					Color = Color.Random,
					LightCookie = Texture.Load( "materials/effects/lightcookie.vtex" )
				};

				light.UseFogNoShadows();
				light.SetModel( Model );
				light.SetupPhysicsFromModel( PhysicsMotionType.Dynamic, false );
				light.Position = tr.EndPos + -light.CollisionBounds.Center + tr.Normal * light.CollisionBounds.Size * 0.5f;

				Undo.AddEntity( Owner, light, "Light" );

				if ( !useRope )
					return;

				var rope = Particles.Create( "particles/rope.vpcf" );
				rope.SetEntity( 0, light, Vector3.Down * 6.5f ); // Should be an attachment point

				var attachEnt = tr.Body.IsValid() ? tr.Body.Entity : tr.Entity;
				var attachLocalPos = tr.Body.Transform.PointToLocal( tr.EndPos ) * (1.0f / tr.Entity.Scale);

				if ( attachEnt.IsWorld )
				{
					rope.SetPosition( 1, attachLocalPos );
				}
				else
				{
					rope.SetEntityBone( 1, attachEnt, tr.Bone, new Transform( attachLocalPos ) );
				}

				var spring = PhysicsJoint.Spring
					.From( light.PhysicsBody, Vector3.Down * 6.5f )
					.To( tr.Body, tr.Body.Transform.PointToLocal( tr.EndPos ) )
					.WithFrequency( 5.0f )
					.WithDampingRatio( 0.7f )
					.WithReferenceMass( light.PhysicsBody.Mass )
					.WithMinRestLength( 0 )
					.WithMaxRestLength( 100 )
					.WithCollisionsEnabled()
					.Create();

				spring.EnableAngularConstraint = false;
				spring.OnBreak( () =>
				{
					rope?.Destroy( true );
					spring.Remove();
				} );
			}
		}
	}
}
