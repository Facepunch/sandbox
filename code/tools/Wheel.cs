using Sandbox.Physics;

namespace Sandbox.Tools;

[Library( "tool_wheel", Title = "Wheel", Description = "A wheel that you can turn on and off (but actually can't yet)", Group = "construction" )]
public partial class WheelTool : BaseTool
{
	PreviewEntity previewModel;

	protected override bool IsPreviewTraceValid( TraceResult tr )
	{
		if ( !base.IsPreviewTraceValid( tr ) )
			return false;

		if ( tr.Entity is WheelEntity )
			return false;

		return true;
	}

	public override void CreatePreviews()
	{
		if ( TryCreatePreview( ref previewModel, "models/citizen_props/wheel01.vmdl" ) )
		{
			previewModel.RotationOffset = Rotation.FromAxis( Vector3.Up, 90 );
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

			if ( !tr.Hit )
				return;

			if ( !tr.Entity.IsValid() )
				return;

			var attached = !tr.Entity.IsWorld && tr.Body.IsValid() && tr.Body.PhysicsGroup != null && tr.Body.GetEntity().IsValid();

			if ( attached && tr.Entity is not Prop )
				return;

			CreateHitEffects( tr.EndPosition, tr.Normal );

			if ( tr.Entity is WheelEntity )
			{
				// TODO: Set properties

				return;
			}

			var ent = new WheelEntity
			{
				Position = tr.EndPosition,
				Rotation = Rotation.LookAt( tr.Normal ) * Rotation.From( new Angles( 0, 90, 0 ) ),
			};

			ent.SetModel( "models/citizen_props/wheel01.vmdl" );

			ent.PhysicsBody.Mass = tr.Body.Mass;
			ent.Joint = PhysicsJoint.CreateHinge( ent.PhysicsBody, tr.Body, tr.EndPosition, tr.Normal );

			Event.Run( "entity.spawned", ent, Owner );
		}
	}
}
