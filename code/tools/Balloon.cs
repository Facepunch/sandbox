namespace Sandbox.Tools
{
	[Library( "tool_balloon", Title = "Balloons", Description = "Create Balloons!", Group = "construction" )]
	public partial class BalloonTool : BaseTool
	{
		[Net]
		public Color32 color { get; set; }

		PreviewEntity previewModel;

		public BalloonTool()
		{
			color = Color.Random.ToColor32();
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
				previewModel.RenderColor = color;
			}

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

				if ( !tr.Hit )
					return;

				if ( !tr.Entity.IsValid() )
					return;

				CreateHitEffects( tr.EndPos );

				if ( tr.Entity is BalloonEntity )
					return;

				var ent = new BalloonEntity
				{
					Position = tr.EndPos,
				};

				ent.SetModel( "models/citizen_props/balloonregular01.vmdl" );
				ent.PhysicsBody.GravityScale = -0.2f;
				ent.RenderColor = color;

				color = Color.Random.ToColor32();

				if ( !useRope )
					return;

				var rope = Particles.Create( "particles/rope.vpcf" );
				rope.SetEntity( 0, ent );

				var attachEnt = tr.Body.IsValid() ? tr.Body.Entity : tr.Entity;
				var attachLocalPos = tr.Body.Transform.PointToLocal( tr.EndPos );

				if ( attachEnt.IsWorld )
				{
					rope.SetPos( 1, attachLocalPos );
				}
				else
				{
					rope.SetEntityBone( 1, attachEnt, tr.Bone, new Transform( attachLocalPos ) );
				}

				ent.AttachRope = rope;

				ent.AttachJoint = PhysicsJoint.Spring
					.From( ent.PhysicsBody )
					.To( tr.Body )
					.WithPivot( tr.EndPos )
					.WithFrequency( 5.0f )
					.WithDampingRatio( 0.7f )
					.WithReferenceMass( 0 )
					.WithMinRestLength( 0 )
					.WithMaxRestLength( 100 )
					.WithCollisionsEnabled()
					.Create();

				SandboxGame.Undos.Insert( 0, new Undo( Owner, ent ) );
			}
		}
	}
}
