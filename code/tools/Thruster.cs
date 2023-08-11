using Sandbox.UI;

namespace Sandbox.Tools
{
	[Library( "tool_thruster", Title = "Thruster", Description = "A rocket type thing that can push forwards and backward", Group = "construction" )]
	public partial class ThrusterTool : BaseTool
	{
		[ConVar.ClientData( "tool_thruster_model" )]
		public static string CurrentModel { get; set; } = "models/thruster/thrusterprojector.vmdl";
		PreviewEntity previewModel;
		bool massless = true;
		public override void CreatePreviews()
		{
			if ( TryCreatePreview( ref previewModel, GetConvarValue( "tool_thruster_model" ) ) )
			{
				previewModel.RotationOffset = Rotation.FromAxis( Vector3.Right, -90 );
			}
		}

		protected override bool IsPreviewTraceValid( TraceResult tr )
		{
			if ( !base.IsPreviewTraceValid( tr ) )
				return false;

			if ( tr.Entity is ThrusterEntity )
				return false;

			return true;
		}

		public override void Simulate()
		{
			if ( previewModel.IsValid() && GetConvarValue( "tool_thruster_model" ) != previewModel.GetModelName() )
			{
				previewModel.SetModel( GetConvarValue( "tool_thruster_model" ) );
			}
			if ( !Game.IsServer )
				return;

			using ( Prediction.Off() )
			{
				if ( Input.Pressed( "attack2" ) )
				{
					massless = !massless;
				}

				if ( !Input.Pressed( "attack1" ) )
					return;

				var tr = DoTrace();

				if ( !tr.Hit )
					return;

				if ( !tr.Entity.IsValid() )
					return;

				var attached = !tr.Entity.IsWorld && tr.Body.IsValid() && tr.Body.PhysicsGroup != null && tr.Body.GetEntity().IsValid();

				if ( attached && tr.Entity is not Prop )
					return;

				CreateHitEffects( tr.EndPosition, tr.Normal );

				if ( tr.Entity is ThrusterEntity )
				{
					// TODO: Set properties

					return;
				}

				var ent = new ThrusterEntity
				{
					Position = tr.EndPosition,
					Rotation = Rotation.LookAt( tr.Normal, Owner.EyeRotation.Forward ) * Rotation.From( new Angles( 90, 0, 0 ) ),
					PhysicsEnabled = !attached,
					EnableSolidCollisions = !attached,
					TargetBody = attached ? tr.Body : null,
					Massless = massless
				};

				if ( attached )
				{
					ent.SetParent( tr.Body.GetEntity(), tr.Body.GroupName );
				}

				ent.SetModel( GetConvarValue( "tool_thruster_model" ) );

				Event.Run( "entity.spawned", ent, Owner );
			}
		}

		public override void CreateToolPanel()
		{
			if ( Game.IsClient )
			{
				var modelSelector = new ModelSelector( new string[] { "thruster" } );
				SpawnMenu.Instance?.ToolPanel?.AddChild( modelSelector );
			}
		}
	}
}
