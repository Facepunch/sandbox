using static VehicleMath;

/// <summary>
/// The driver's keys, turned into throttle, brake, steering, boost and handbrake controls.
/// </summary>
public sealed partial class FourWheelVehicle
{
	/// <summary>
	/// Slower than this (in/s) the car counts as stopped, and the throttle drives rather than brakes.
	/// </summary>
	private const float SpeedThrottleAsBrake = 2.0f;

	/// <summary>
	/// How fast the brake comes on, per second. Twice as fast braking with forward while rolling back.
	/// </summary>
	private const float BrakeRate = 1.5f;
	private const float BrakeBackForwardScalar = 2.0f;

	private VehicleControlParams _controls = new() { Handbrake = true };
	private bool _throttleDown;

	/// <summary>
	/// Throttle held with the handbrake on: revving the engine in neutral.
	/// </summary>
	private bool _freeRevving;

	private struct DriverInput
	{
		public bool Forward;
		public bool Back;
		public bool Left;
		public bool Right;
		public bool Boost;
		public bool Handbrake;
	}

	private void ResetControls()
	{
		_controls = new VehicleControlParams { Handbrake = true };
		_throttleDown = false;
		_freeRevving = false;
	}

	/// <summary>
	/// The boost is burning.
	/// </summary>
	public bool IsBoosting() => (_state.BoostDelay - Engine.BoostDelay) > 0.0f;

	/// <summary>
	/// Recharging from a boost, so another can't start yet.
	/// </summary>
	private bool IsBoostRecovering => _state.BoostTimeLeft != 100 && _state.BoostTimeLeft != 0;

	/// <summary>
	/// Let go of the steering: it returns to centre, slower the faster we're going.
	/// </summary>
	private void SteeringRest( float carSpeed, float frameTime )
	{
		var steeringRate = RemapValClamped( carSpeed, Steering.SpeedSlow, Steering.SpeedFast,
			Steering.SteeringRestRateSlow, Steering.SteeringRestRateFast );
		_controls.Steering = Approach( 0, _controls.Steering, steeringRate * frameTime );
	}

	/// <summary>
	/// Steering towards full lock, slower the faster we're going - but snapping back through the centre when changing
	/// direction, quicker on the brakes and while boosting.
	/// </summary>
	private void SteeringTurn( float carSpeed, bool turnLeft, bool brake, bool throttle, float frameTime )
	{
		var targetSteering = turnLeft ? -1.0f : 1.0f;

		// steering speeds are in mph, engine speeds in in/s
		var steeringRestRate = RemapValClamped( carSpeed, Steering.SpeedSlow, Steering.SpeedFast,
			Steering.SteeringRestRateSlow, Steering.SteeringRestRateFast );

		var carSpeedIns = MphToIns( carSpeed );
		if ( carSpeedIns > MaxSpeedIns )
		{
			steeringRestRate = RemapValClamped( carSpeedIns, MaxSpeedIns, BoostMaxSpeedIns,
				Steering.SteeringRestRateFast, Steering.SteeringRestRateFast * 0.5f );
		}

		// still going faster than max after a boost, steer like we're boosting
		var isBoosting = _state.IsTorqueBoosting || (IsBoostRecovering && carSpeedIns > MaxSpeedIns * Engine.AutobrakeSpeedGain);

		if ( isBoosting )
		{
			steeringRestRate *= Steering.BoostSteeringRestRateFactor;
		}
		else if ( throttle )
		{
			steeringRestRate *= Steering.ThrottleSteeringRestRateFactor;
		}

		var steeringRate = RemapValClamped( carSpeed, Steering.SpeedSlow, Steering.SpeedFast,
			Steering.SteeringRateSlow, Steering.SteeringRateFast );

		if ( MathF.Abs( steeringRate ) < steeringRestRate && Sign( targetSteering ) != Sign( _controls.Steering ) )
		{
			steeringRate = steeringRestRate;
		}

		if ( isBoosting )
		{
			steeringRate *= Steering.BoostSteeringRateFactor;
		}
		else if ( brake )
		{
			steeringRate *= Steering.BrakeSteeringRateFactor;
		}

		_controls.Steering = Approach( targetSteering, _controls.Steering, steeringRate * frameTime );
	}

	private void UpdateDriverControls( DriverInput input, float frameTime )
	{
		// which way we're going, if we're really going anywhere
		var carSign = _state.Speed >= SpeedThrottleAsBrake ? 1.0f : _state.Speed <= -SpeedThrottleAsBrake ? -1.0f : 0.0f;
		var carSpeed = MathF.Abs( InsToMph( _state.Speed ) );

		if ( input.Left || input.Right )
		{
			SteeringTurn( carSpeed, input.Left, input.Back, input.Forward && !input.Back, frameTime );
		}
		else
		{
			SteeringRest( carSpeed, frameTime );
		}

		_controls.Boost = 0;
		_controls.Handbrake = false;
		_controls.HandbrakeLeft = false;
		_controls.HandbrakeRight = false;
		bool throttle;

		if ( input.Forward )
		{
			throttle = true;

			// less throttle while turning, ramped in from a stop so it can still pull away
			_controls.Throttle = 1.0f;
			if ( _controls.Steering != 0 )
			{
				var throttleReduce = carSpeed < Steering.SpeedSlow
					? RemapValClamped( carSpeed, 0, Steering.SpeedSlow, 0, Steering.TurnThrottleReduceSlow )
					: RemapValClamped( carSpeed, Steering.SpeedSlow, Steering.SpeedFast, Steering.TurnThrottleReduceSlow, Steering.TurnThrottleReduceFast );

				_controls.Throttle = MathF.Max( 0, 1.0f - (throttleReduce * MathF.Abs( _controls.Steering )) );
			}

			// rolling backwards: forward is the brake
			if ( carSign < 0.0f )
			{
				_controls.Brake = Approach( 1.0f, _controls.Brake, frameTime * BrakeRate * BrakeBackForwardScalar );
				_controls.Throttle = 0.0f;
				throttle = false;
			}
			else
			{
				_controls.Brake = 0.0f;
			}
		}
		else if ( input.Back )
		{
			throttle = true;
			_controls.Throttle = -1.0f;

			// rolling forwards: back is the brake
			if ( carSign > 0.0f )
			{
				_controls.Brake = Approach( 1.0f, _controls.Brake, frameTime * BrakeRate );
				_controls.Throttle = 0.0f;
				throttle = false;
			}
			else
			{
				_controls.Brake = 0.0f;
			}
		}
		else
		{
			throttle = false;
			_controls.Throttle = 0;
			_controls.Brake = 0.0f;
		}

		// boost only going forward, and not while it's recharging
		if ( input.Boost && throttle && !IsBoostRecovering && !input.Back )
		{
			_controls.Boost = 1.0f;
		}

		if ( input.Handbrake )
		{
			_controls.Handbrake = true;
			_controls.HandbrakeLeft = input.Left;
			_controls.HandbrakeRight = !input.Left && input.Right;

			// no engine rev up while braking
			throttle = false;
		}

		_throttleDown = throttle;
		_freeRevving = _controls.Handbrake && (input.Forward || input.Back);
	}
}
