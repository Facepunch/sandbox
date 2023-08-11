
using Sandbox;

namespace Sandbox
{
	[Library]
	public partial class PlayerWalkController : WalkController
	{
		// These 3 methods are copied from WalkController
		public override void StepMove()
		{
			MoveHelper mover = new MoveHelper( Position, Velocity );
			mover.Trace = mover.Trace
				.Size( mins, maxs )
				.Ignore( Pawn )
				.OnTraceEvent( Pawn ); // SandboxPlus addition for Stargate support
			mover.MaxStandableAngle = GroundAngle;

			mover.TryMoveWithStep( Time.Delta, StepSize );

			Position = mover.Position;
			Velocity = mover.Velocity;
		}

		public override void Move()
		{
			MoveHelper mover = new MoveHelper( Position, Velocity );
			mover.Trace = mover.Trace
				.Size( mins, maxs )
				.Ignore( Pawn )
				.OnTraceEvent( Pawn ); // SandboxPlus addition for Stargate support
			mover.MaxStandableAngle = GroundAngle;

			mover.TryMove( Time.Delta );

			Position = mover.Position;
			Velocity = mover.Velocity;
		}

		public override TraceResult TraceBBox( Vector3 start, Vector3 end, Vector3 mins, Vector3 maxs, float liftFeet = 0.0f )
		{
			if ( liftFeet > 0 )
			{
				start += Vector3.Up * liftFeet;
				maxs = maxs.WithZ( maxs.z - liftFeet );
			}

			var trace = Trace.Ray( start + TraceOffset, end + TraceOffset )
						.Size( mins, maxs )
						.WithAnyTags( "solid", "playerclip", "passbullets", "player" )
						.Ignore( Pawn )
						.OnTraceEvent( Pawn ); // SandboxPlus addition for Stargate support

			var tr = trace.Run();

			tr.EndPosition -= TraceOffset;
			return tr;
		}
	}
}

public static class TraceExtensions
{
	public static Trace OnTraceEvent( this Trace instance, Entity ent )
	{
		// As Trace is an immutable struct, any modifications will result in a new Trace object that callers will need to pass into the returnFn(newTrace)
		Event.Run( "trace.prepare", instance, ent, ( Trace newTrace ) =>
		{
			instance = newTrace;
		} );
		return instance;
	}
}
