using Sandbox.Npcs.Layers;

namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Task that commands the NavigationLayer to move to a target position or GameObject.
/// When tracking a GameObject, re-evaluates the path periodically.
/// Does not override the NPC's look target — but will rotate the body to face the
/// movement direction when the angle would otherwise cause silly walking
/// </summary>
public class MoveTo : TaskBase
{
	public Vector3? TargetPosition { get; set; }
	public GameObject TargetObject { get; set; }
	public float StopDistance { get; set; } = 10f;
	public float ReevaluateInterval { get; set; } = 0.5f;
	public float LateralThreshold { get; set; } = 60f;

	private TimeSince _lastReevaluate;

	public MoveTo( Vector3 targetPosition, float stopDistance = 10f )
	{
		TargetPosition = targetPosition;
		StopDistance = stopDistance;
	}

	public MoveTo( GameObject targetObject, float stopDistance = 10f )
	{
		TargetObject = targetObject;
		StopDistance = stopDistance;
	}

	protected override void OnStart()
	{
		var pos = GetTargetPosition();
		if ( !pos.HasValue ) return;

		Npc.Navigation.MoveTo( pos.Value, StopDistance );
		_lastReevaluate = 0;
	}

	protected override TaskStatus OnUpdate()
	{
		// Target object destroyed mid-move
		if ( TargetObject is not null && !TargetObject.IsValid() )
			return TaskStatus.Failed;

		// Re-evaluate path for moving targets
		if ( TargetObject.IsValid() && _lastReevaluate > ReevaluateInterval )
		{
			var pos = GetTargetPosition();
			if ( pos.HasValue )
				Npc.Navigation.MoveTo( pos.Value, StopDistance );
			_lastReevaluate = 0;
		}

		var agent = Npc.Navigation.Agent;
		if ( agent.IsValid() && agent.Velocity.WithZ( 0 ).Length > 1f )
		{
			var moveDir = agent.Velocity.WithZ( 0 ).Normal;
			var fwd = Npc.WorldRotation.Forward.WithZ( 0 ).Normal;
			var angle = Vector3.GetAngle( fwd, moveDir );

			if ( angle > LateralThreshold && !Npc.Animation.LookTarget.HasValue )
			{
				// No look target — face the movement direction
				var targetRot = Rotation.LookAt( moveDir, Vector3.Up );
				Npc.GameObject.WorldRotation = Rotation.Lerp(
					Npc.WorldRotation, targetRot, Npc.Animation.LookSpeed * Time.Delta );
			}
		}

		return Npc.Navigation.GetStatus();
	}

	private Vector3? GetTargetPosition()
	{
		if ( TargetObject.IsValid() )
		{
			// Navigate to the closest point on the object's bounds, not its origin.
			// This prevents the NPC from trying to walk inside large props.
			var bounds = TargetObject.GetBounds();
			return bounds.ClosestPoint( Npc.WorldPosition );
		}

		return TargetPosition;
	}
}
