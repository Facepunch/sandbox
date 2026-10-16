public partial class Physgun
{
	/// <summary>
	/// Half-angle, in degrees, of the cone player aim has to stay within to keep pulling a locked object.
	/// </summary>
	static float PullConeAngle => 35.0f;

	/// <summary>
	/// Within this many degrees of the object the pull runs at full strength, fading out towards <see cref="PullConeAngle"/>.
	/// </summary>
	static float PullConeFullStrengthAngle => 10.0f;

	/// <summary>
	/// How far in front of player aim the cone's apex is placed when we lock onto an object.
	/// </summary>
	static float PullConeApexDistance => 16.0f;

	/// <summary>
	/// Once a gravity pull finds an object it locks onto it, and keeps pulling while our aim stays within
	/// a cone from an apex in front of us to the object. The apex follows our position.
	/// </summary>
	GrabState UpdatePullLock( GrabState locked, GrabState found, Transform aim )
	{
		if ( IsPullLock( locked ) )
		{
			var strength = GetPullStrength( locked, aim );
			if ( strength > 0 )
			{
				var state = locked with
				{
					GrabOffset = aim.Rotation.Inverse * locked.GameObject.WorldRotation,
					PullStrength = strength
				};

				_spinRotation = state.GrabOffset;
				_snapRotation = _spinRotation;

				return state;
			}
		}

		if ( !found.IsValid() ) return found;

		found.PullApex = aim.Forward * PullConeApexDistance;
		found.PullStrength = GetPullStrength( found, aim );
		return found;
	}

	static bool IsPullLock( GrabState state )
	{
		return state.IsValid() && state.Pulling && state.Body.IsValid() && state.Body.MotionEnabled;
	}

	/// <summary>
	/// How strongly we pull, from 1 when aiming at the object down to 0 at the edge of the cone.
	/// </summary>
	static float GetPullStrength( GrabState state, Transform aim )
	{
		var apex = aim.Position + state.PullApex;

		// Measure against the part of the object nearest our aim, so the cone always covers the whole object.
		var aimPoint = apex + aim.Forward * apex.Distance( state.EndPoint );
		var target = state.Body.FindClosestPoint( aimPoint );

		var angle = Vector3.GetAngle( aim.Forward, target - apex );
		if ( angle >= PullConeAngle ) return 0;

		return 1 - MathX.LerpInverse( angle, PullConeFullStrengthAngle, PullConeAngle );
	}

	/// <summary>
	/// Pulls the locked object along the cone, towards its apex.
	/// </summary>
	void ApplyPullForce( GrabState state )
	{
		var apex = CurrentAimTransform.Position + state.PullApex;
		var force = (apex - state.EndPoint).Normal * state.Body.Mass * PullForce * state.PullStrength;
		state.Body.ApplyForceAt( state.EndPoint, force );
	}
}
