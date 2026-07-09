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

	/// <summary>
	/// Keep facing this object while moving instead of turning into the movement
	/// direction. The NPC backpedals or strafes -- short moves away from someone
	/// happen without turning our back on them.
	/// </summary>
	public GameObject FaceTarget { get; set; }

	private TimeSince _lastReevaluate;
	private bool _restoreFaceMovement;

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
		if ( FaceTarget.IsValid() && Npc.Navigation.FaceMovementDirection )
		{
			Npc.Navigation.FaceMovementDirection = false;
			_restoreFaceMovement = true;
		}

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

		// Turn toward whoever we're keeping our front to while we move
		if ( FaceTarget.IsValid() )
		{
			var dir = (FaceTarget.WorldPosition - Npc.WorldPosition).WithZ( 0 );
			if ( dir.Length > 1f )
			{
				var targetRotation = Rotation.LookAt( dir.Normal, Vector3.Up );
				Npc.WorldRotation = Rotation.Slerp( Npc.WorldRotation, targetRotation, Npc.Navigation.TurnSpeed * Time.Delta );
			}
		}

		return Npc.Navigation.GetStatus();
	}

	protected override void OnEnd()
	{
		if ( _restoreFaceMovement )
		{
			Npc.Navigation.FaceMovementDirection = true;
			_restoreFaceMovement = false;
		}
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
