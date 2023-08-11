using Sandbox.Physics;

namespace Sandbox.Tools;

[Library( "tool_balloon", Title = "Balloons", Description = "Create Balloons!", Group = "construction" )]
public partial class BalloonTool : BaseTool
{
	[Net]
	public Color Tint { get; set; }

	PreviewEntity previewModel;

	public override void Activate()
	{
		base.Activate();

		if ( Game.IsServer )
		{
			Tint = Color.Random;
		}
	}

	protected override bool IsPreviewTraceValid( TraceResult tr )
	{
		if ( !base.IsPreviewTraceValid( tr ) )
			return false;

		if ( tr.Entity is BalloonEntity )
			return false;

		return true;
	}

	public override void CreatePreviews()
	{
		if ( TryCreatePreview( ref previewModel, "models/citizen_props/balloonregular01.vmdl" ) )
		{
			previewModel.RelativeToNormal = false;
		}
	}

	public override void Simulate()
	{
		if ( previewModel.IsValid() )
		{
			previewModel.RenderColor = Tint;
		}

		if ( !Game.IsServer )
			return;

		using ( Prediction.Off() )
		{
			bool useRope = Input.Pressed( "attack1" );
			if ( !useRope && !Input.Pressed( "attack2" ) )
				return;

			var tr = DoTrace();

			if ( !tr.Hit )
				return;

			if ( !tr.Entity.IsValid() )
				return;

			CreateHitEffects( tr.EndPosition, tr.Normal );

			if ( tr.Entity is BalloonEntity )
				return;

			var ent = new BalloonEntity
			{
				Position = tr.EndPosition,
			};

			ent.SetModel( "models/citizen_props/balloonregular01.vmdl" );
			ent.PhysicsBody.GravityScale = -0.2f;
			ent.RenderColor = Tint;

			Tint = Color.Random;

			if ( !useRope )
				return;

			var rope = Particles.Create( "particles/rope.vpcf" );
			rope.SetEntity( 0, ent );

			var attachEnt = tr.Body.IsValid() ? tr.Body.GetEntity() : tr.Entity;
			var attachLocalPos = tr.Body.Transform.PointToLocal( tr.EndPosition ) * (1.0f / tr.Entity.Scale);

			if ( attachEnt.IsWorld )
			{
				rope.SetPosition( 1, attachLocalPos );
			}
			else
			{
				rope.SetEntityBone( 1, attachEnt, tr.Bone, new Transform( attachLocalPos ) );
			}

			var spring = PhysicsJoint.CreateLength( ent.PhysicsBody, PhysicsPoint.World( tr.Body, tr.EndPosition ), 100 );
			spring.SpringLinear = new( 5, 0.7f );
			spring.Collisions = true;
			spring.EnableAngularConstraint = false;
			spring.OnBreak += () =>
			{
				rope?.Destroy( true );
				spring.Remove();
			};
			Event.Run( "entity.spawned", ent, Owner );
		}
	}
}
