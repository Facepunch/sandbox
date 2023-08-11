namespace Sandbox.Tools
{
	[Library( "tool_lamp", Title = "Lamps", Description = "Directional light source that casts shadows", Group = "construction" )]
	public partial class LampTool : BaseTool
	{
		PreviewEntity previewModel;

		private string Model => "models/torch/torch.vmdl";

		protected override bool IsPreviewTraceValid( TraceResult tr )
		{
			if ( !base.IsPreviewTraceValid( tr ) )
				return false;

			if ( tr.Entity is LampEntity )
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
			if ( !Game.IsServer )
				return;

			using ( Prediction.Off() )
			{
				if ( !Input.Pressed( "attack1" ) )
					return;

				var tr = DoTrace();

				if ( !tr.Hit || !tr.Entity.IsValid() )
					return;

				CreateHitEffects( tr.EndPosition, tr.Normal );

				if ( tr.Entity is LampEntity lamp )
				{
					// TODO: Set properties

					lamp.Flicker = !lamp.Flicker;

					return;
				}

				lamp = new LampEntity
				{
					Enabled = true,
					DynamicShadows = true,
					Range = 512,
					Falloff = 1.0f,
					LinearAttenuation = 0.0f,
					QuadraticAttenuation = 1.0f,
					InnerConeAngle = 25,
					OuterConeAngle = 45,
					Brightness = 10,
					Color = Color.Random,
					Rotation = Rotation.Identity,
					LightCookie = Texture.Load( "materials/effects/lightcookie.vtex" )
				};

				lamp.SetModel( Model );
				lamp.SetupPhysicsFromModel( PhysicsMotionType.Dynamic, false );
				lamp.Position = tr.EndPosition + -lamp.CollisionBounds.Center + tr.Normal * lamp.CollisionBounds.Size * 0.5f;

				Event.Run( "entity.spawned", lamp, Owner );
			}
		}
	}
}
