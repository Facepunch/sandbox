using Sandbox.Physics;
using Sandbox.Rendering;

public partial class Physgun
{
	[Property, RequireComponent] public HighlightOutline BeamHighlight { get; set; }

	[Property, Group( "Sound" )] SoundEvent ReleasedSound { get; set; }
	[Property, Group( "Sound" )] SoundEvent ButtonInSound { get; set; }
	[Property, Group( "Sound" )] SoundEvent ButtonOutSound { get; set; }

	[Property] public float Range { get; set; } = 8196f;

	public struct GrabState
	{
		public bool Active { get; set; }
		public bool Pulling { get; set; }
		public GameObject GameObject { get; set; }
		public Vector3 LocalOffset { get; set; }
		public Vector3 LocalNormal { get; set; }
		public Rotation GrabOffset { get; set; }
		public float GrabDistance { get; set; }

		public readonly Vector3 EndPoint
		{
			get
			{
				if ( !GameObject.IsValid() ) return LocalOffset;
				return GameObject.WorldTransform.PointToWorld( LocalOffset );
			}
		}

		public readonly Vector3 EndNormal
		{
			get
			{
				if ( !GameObject.IsValid() ) return LocalNormal;
				return GameObject.WorldTransform.NormalToWorld( LocalNormal );
			}
		}

		public readonly bool IsValid() => GameObject.IsValid();

		public readonly Rigidbody Body => GameObject?.GetComponent<Rigidbody>();

		public override readonly int GetHashCode()
		{
			return HashCode.Combine(
				Active,
				Pulling,
				GameObject,
				LocalOffset,
				LocalNormal,
				GrabOffset,
				GrabDistance
			);
		}
	}

	[Sync] public GrabState _state { get; set; } = default;

	[Sync] public GrabState _stateHovered { get; set; } = default;

	Transform _lastAimTransform;
	Transform CurrentAimTransform => HasOwner ? AimTransform : _lastAimTransform;

	bool _preventReselect = false;

	bool _isSpinning;
	bool _isSnapping;
	Rotation _spinRotation;
	Rotation _snapRotation;

	bool _launched;

	/// <summary>
	/// The force applied to pull objects to us.
	/// </summary>
	static float PullForce => 1000.0f;

	/// <summary>
	/// The force applied when launching grabbed objects.
	/// </summary>
	static float LaunchForce => 2000.0f;

	/// <summary>
	/// The distance at which we'll grab an object when pulling it towards us.
	/// </summary>
	static float PullDistance => 200.0f;

	public override void OnCameraMove( Player player, ref Angles angles )
	{
		base.OnCameraMove( player, ref angles );

		if ( _state.IsValid() && _isSpinning )
		{
			angles = default;
		}
	}

	protected override void OnPreRender()
	{
		base.OnPreRender();

		if ( _state.Active && !_state.Pulling )
		{
			var muzzle = HasOwner ? MuzzleTransform.WorldTransform : CurrentAimTransform;
			UpdateBeam( muzzle, _state.EndPoint, _stateHovered.EndNormal, _state.IsValid() );
		}
		else
		{
			CloseBeam();
		}
	}

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		_lastAimTransform = AimTransform;

		UpdateViewmodelScreen();
		UpdateScreenGraph();

		if ( Scene.TimeScale == 0 )
			return;

		if ( Input.Pressed( "use" ) && _state.IsValid() )
		{
			ViewModel?.PlaySound( ButtonInSound );
		}
		else if ( Input.Released( "use" ) && _state.IsValid() )
		{
			ViewModel?.PlaySound( ButtonOutSound );
		}

		_isSpinning = Input.Down( "use" ) && _state.IsValid();
		if ( _isSpinning )
		{
			Input.Clear( "use" );
		}

		var isSnapping = Input.Down( "run" ) || Input.Down( "walk" );
		var snapAngle = Input.Down( "walk" ) ? 15.0f : 45.0f;
		if ( !isSnapping && _isSnapping ) _spinRotation = _snapRotation;
		_isSnapping = isSnapping;

		var isPulling = Input.Down( "attack2" ) && !_preventReselect;

		ViewModel?.RunEvent<ViewModel>( UpdateViewModel );

		_stateHovered = default;

		if ( _state.IsValid() )
		{
			if ( _state.Pulling )
			{
				if ( Input.Pressed( "attack1" ) )
				{
					var force = player.EyeTransform.Rotation.Forward * LaunchForce;
					Launch( _state.Body, force );

					_state = default;
					_preventReselect = true;
				}
				else if ( Input.Pressed( "attack2" ) )
				{
					_state = default;
					_preventReselect = true;
				}
			}
			else
			{
				if ( !Input.Down( "attack1" ) )
				{
					_state = default;
					_preventReselect = true;
					ViewModel?.PlaySound( ReleasedSound );
					return;
				}

				if ( Input.Down( "attack2" ) )
				{
					Freeze( _state.Body );
					_state = default;
					_preventReselect = true;
					ViewModel?.PlaySound( ReleasedSound );
					return;
				}

				if ( !Input.MouseWheel.IsNearZeroLength )
				{
					var state = _state;
					state.GrabDistance += Input.MouseWheel.y * 20.0f;
					state.GrabDistance = MathF.Max( 0.0f, state.GrabDistance );

					_state = default;
					_state = state;

					// stop processing this so inventory doesn't change
					Input.MouseWheel = default;
				}
			}

			if ( _isSpinning )
			{
				var look = Input.AnalogLook * -1;

				if ( _isSnapping )
				{
					if ( MathF.Abs( look.yaw ) > MathF.Abs( look.pitch ) ) look.pitch = 0;
					else look.yaw = 0;
				}

				_spinRotation = Rotation.From( look ) * _spinRotation;
				var spinRotation = _spinRotation;

				if ( _isSnapping )
				{
					var eyeRotation = _state.Pulling
						? player.EyeTransform.Rotation
						: Rotation.FromYaw( player.Controller.EyeAngles.yaw );

					// convert rotation to worldspace
					spinRotation = eyeRotation * spinRotation;

					// snap angles in worldspace
					var angles = spinRotation.Angles();
					spinRotation = angles.SnapToGrid( snapAngle );

					// convert rotation back to localspace
					spinRotation = eyeRotation.Inverse * spinRotation;
				}

				// save snap rotation so it can be applied after snap has finished
				_snapRotation = spinRotation;

				var state = _state;
				state.GrabOffset = spinRotation;

				// State needs to reset for sync to detect a change, bug or how it's meant to work?
				_state = default;
				_state = state;
			}

			return;
		}
		else
		{
			_state = default;
		}

		if ( _preventReselect )
		{
			if ( !Input.Down( "attack1" ) && !Input.Down( "attack2" ) )
				_preventReselect = false;

			return;
		}

		FindGrabbedBody( out var sh, player.EyeTransform, player.Controller.EyeAngles.yaw, isPulling );
		_stateHovered = sh;

		if ( sh.IsValid() && sh.Pulling && sh.Body.MotionEnabled )
		{
			var eyePosition = player.EyeTransform.Position;
			var closest = sh.Body.FindClosestPoint( eyePosition );
			var distance = closest.Distance( eyePosition );

			if ( distance <= PullDistance )
			{
				_state = sh with { Active = true, Pulling = true, };
			}
		}

		if ( _state.Pulling || _stateHovered.Pulling )
			return;

		if ( Input.Down( "attack1" ) )
		{
			ViewModel?.RunEvent<ViewModel>( x => x.OnAttack() );

			var muzzle = WeaponModel?.MuzzleTransform?.WorldTransform ?? player.EyeTransform;

			_state = _stateHovered with { Active = true, Pulling = false };

			if ( _state.IsValid() )
			{
				Unfreeze( _state.Body );
			}
		}
		else if ( Input.Released( "attack1" ) )
		{
			ViewModel?.PlaySound( ReleasedSound );
		}
		else if ( Input.Pressed( "reload" ) )
		{
			if ( _stateHovered.IsValid() )
			{
				UnfreezeAll( _stateHovered.Body );
			}
		}
		else
		{
			_state = default;
			_preventReselect = false;
		}
	}

	/// <summary>
	/// Seat / standalone input — ShootInput grabs, SecondaryInput pulls.
	/// </summary>
	public void OnControl()
	{
		if ( HasOwner ) return;

		var aim = AimTransform;
		_lastAimTransform = aim;
		var isPulling = SecondaryInput.Down() && !_preventReselect;

		_stateHovered = default;

		if ( _state.IsValid() )
		{
			if ( _state.Pulling )
			{
				// Left-click while pulling punts the object forward
				if ( ShootInput.Pressed() )
				{
					var force = aim.Rotation.Forward * LaunchForce;
					Launch( _state.Body, force );
					_state = default;
					_preventReselect = true;
				}
				// Right-click cancels the pull
				else if ( SecondaryInput.Pressed() )
				{
					_state = default;
					_preventReselect = true;
				}
			}
			else
			{
				// Release grab when primary is let go
				if ( !ShootInput.Down() )
				{
					_state = default;
					_preventReselect = true;
					GameObject.PlaySound( ReleasedSound );
					return;
				}

				// Retract / extend grabbed object
				if ( ExtendInput.Down() || RetractInput.Down() )
				{
					var state = _state;
					if ( ExtendInput.Down() ) state.GrabDistance += 200.0f * Time.Delta;
					if ( RetractInput.Down() ) state.GrabDistance -= 200.0f * Time.Delta;
					state.GrabDistance = MathF.Max( 0.0f, state.GrabDistance );

					_state = default;
					_state = state;
				}
			}

			return;
		}
		else
		{
			_state = default;
		}

		if ( _preventReselect )
		{
			if ( !ShootInput.Down() && !SecondaryInput.Down() )
				_preventReselect = false;

			return;
		}

		FindGrabbedBody( out var sh, aim, aim.Rotation.Yaw(), isPulling );
		_stateHovered = sh;

		if ( sh.IsValid() && sh.Pulling && sh.Body.MotionEnabled )
		{
			var closest = sh.Body.FindClosestPoint( aim.Position );
			if ( closest.Distance( aim.Position ) <= PullDistance )
			{
				_state = sh with { Active = true, Pulling = true };
			}
		}

		if ( _state.Pulling || _stateHovered.Pulling )
			return;

		if ( ShootInput.Down() )
		{
			_state = _stateHovered with { Active = true, Pulling = false };

			if ( _state.IsValid() )
				Unfreeze( _state.Body );
		}
		else if ( ShootInput.Released() )
		{
			GameObject.PlaySound( ReleasedSound );
		}
		else
		{
			_state = default;
			_preventReselect = false;
		}
	}

	private void UpdateViewModel( ViewModel model )
	{
		float stylus = 0;

		if ( _stateHovered.IsValid() )
			stylus = 0.5f;

		if ( _state.Active )
			stylus = 1;

		model.IsAttacking = _state.Active;
		model.Renderer?.Set( "stylus", stylus );
		model.Renderer?.Set( "b_button", _isSpinning );
		model.Renderer?.Set( "brake", _state.Active || _state.Pulling || _stateHovered.Pulling ? 1 : 0 );
	}

	Sandbox.Physics.ControlJoint _joint;
	PhysicsBody _body;

	void RemoveJoint()
	{
		_joint?.Remove();
		_joint = null;

		_body?.Remove();
		_body = null;
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		RemoveJoint();
		CloseBeam();

		_state = default;
		_stateHovered = default;
		_launched = default;
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( !CanMove( _state ) )
		{
			RemoveJoint();

			if ( CanMove( _stateHovered ) && _stateHovered.Pulling )
			{
				var force = CurrentAimTransform.Rotation.Backward * _stateHovered.Body.Mass * PullForce;
				_stateHovered.Body.ApplyForceAt( _stateHovered.EndPoint, force );
			}

			_launched = false;

			return;
		}

		// If we just launched, don't add a joint until state has let go.
		if ( _launched ) return;

		_body ??= new PhysicsBody( Scene.PhysicsWorld ) { BodyType = PhysicsBodyType.Keyframed, AutoSleep = false };

		var eyeTransform = CurrentAimTransform;
		var grabDistance = ClampGrabDistance( _state.Body, _state.EndPoint, eyeTransform, _state.GrabDistance );
		var targetPosition = eyeTransform.Position + eyeTransform.Rotation.Forward * grabDistance;
		var targetRotation = _state.Pulling ? eyeTransform.Rotation * _state.GrabOffset : Rotation.FromYaw( eyeTransform.Rotation.Yaw() ) * _state.GrabOffset;
		_body.Transform = new Transform( targetPosition, targetRotation );

		if ( _joint is null )
		{
			// Scale is built into physics, remove it.
			var bodyTransform = _state.Body.WorldTransform.WithScale( 1.0f );

			var body = _state.Body.PhysicsBody;
			var point1 = new PhysicsPoint( _body );
			var point2 = new PhysicsPoint( body, bodyTransform.PointToLocal( _state.EndPoint ) );
			var maxForce = body.Mass * body.World.Gravity.LengthSquared;

			_joint = PhysicsJoint.CreateControl( point1, point2 );
			_joint.LinearSpring = new PhysicsSpring( 32, 4, maxForce );
			_joint.AngularSpring = new PhysicsSpring( 64, 4, maxForce * 3 );
		}
	}

	/// <summary>
	/// When true, the physgun aims where the seated player's camera looks.
	/// </summary>
	[Property, ClientEditable, Sync] public bool CanAim { get; set; } = true;

	public override bool IsTargetedAim => CanAim;

	Transform AimTransform
	{
		get
		{
			var ray = AimRay;
			return new Transform( ray.Position, Rotation.LookAt( ray.Forward ) );
		}
	}

	bool CanMove( GrabState state )
	{
		if ( !state.IsValid() ) return false;
		if ( !state.Body.IsValid() ) return false;

		// Only move the body if we own it.
		if ( state.Body.IsProxy ) return false;

		// Only move the body if it's dynamic.
		if ( !state.Body.MotionEnabled ) return false;
		if ( !state.Body.PhysicsBody.IsValid() ) return false;

		return true;
	}

	bool FindGrabbedBody( out GrabState state, Transform aim, float yaw, bool isPulling )
	{
		state = default;

		var tr = Scene.Trace.Ray( aim.Position, aim.Position + aim.Forward * Range )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.Run();

		state.LocalOffset = tr.EndPosition;
		state.LocalNormal = tr.Normal;
		state.Pulling = isPulling;

		if ( !tr.Hit || tr.Body is null ) return false;
		if ( tr.Component is not Rigidbody ) return false;

		var go = tr.Body.GameObject;
		if ( !go.IsValid() || go.IsDestroyed ) return false;

		// Ask the object if it allows being grabbed (Ownable and others can reject via IPhysgunEvent)
		var grabEvent = new IPhysgunEvent.GrabEvent { Grabber = Network.Owner };
		go.Root.RunEvent<IPhysgunEvent>( x => x.OnPhysgunGrab( grabEvent ) );
		if ( grabEvent.Cancelled ) return false;

		// Trace hits physics, convert to local using scaled physics transform.
		var bodyTransform = tr.Body.Transform.WithScale( go.WorldScale );

		state.GameObject = go;
		state.LocalNormal = bodyTransform.NormalToLocal( tr.Normal );

		if ( isPulling )
		{
			// Scale is built into mass center, remove it.
			var bodyScale = new Transform( Vector3.Zero, Rotation.Identity, bodyTransform.Scale );
			state.LocalOffset = bodyScale.PointToLocal( tr.Body.LocalMassCenter );
			state.GrabDistance = 0;
			state.GrabOffset = aim.Rotation.Inverse * bodyTransform.Rotation;
		}
		else
		{
			state.LocalOffset = bodyTransform.PointToLocal( tr.HitPosition );
			state.GrabDistance = Vector3.DistanceBetween( aim.Position, tr.HitPosition );
			state.GrabDistance = ClampGrabDistance( state.Body, tr.HitPosition, aim, state.GrabDistance );
			state.GrabOffset = Rotation.FromYaw( yaw ).Inverse * bodyTransform.Rotation;
		}

		_spinRotation = state.GrabOffset;
		_snapRotation = _spinRotation;

		return true;
	}

	static float ClampGrabDistance( Rigidbody body, Vector3 point, Transform eye, float distance, float min = 50.0f )
	{
		distance = MathF.Max( 0.0f, distance );
		var closest = body.FindClosestPoint( eye.Position );
		var along = distance + Vector3.Dot( closest - point, eye.Rotation.Forward );
		return along < min ? distance + (min - along) : distance;
	}

	[Rpc.Broadcast]
	void Freeze( Rigidbody body )
	{
		if ( !body.IsValid() ) return;

		var effect = FreezeEffectPrefab.Clone( body.WorldTransform );

		foreach ( var emitter in effect.GetComponentsInChildren<ParticleModelEmitter>() )
		{
			emitter.Target = body.GameObject;
		}

		if ( body.IsProxy ) return;

		if ( Networking.IsHost )
		{
			body.MotionEnabled = false;
		}
	}

	[Rpc.Host]
	void Unfreeze( Rigidbody body )
	{
		if ( !body.IsValid() ) return;
		if ( body.IsProxy ) return;

		body.MotionEnabled = true;
	}

	[Rpc.Host]
	void UnfreezeAll( Rigidbody body )
	{
		if ( !body.IsValid() ) return;
		if ( body.IsProxy ) return;

		var bodies = new HashSet<Rigidbody>();
		GetConnectedBodies( body.GameObject, bodies );

		var effect = UnFreezeEffectPrefab.Clone( body.WorldTransform );
		foreach ( var emitter in effect.GetComponentsInChildren<ParticleModelEmitter>() )
		{
			emitter.Target = body.GameObject;
		}

		foreach ( var rb in bodies )
		{
			Unfreeze( rb );
		}
	}

	[Rpc.Host]
	void Launch( Rigidbody body, Vector3 force )
	{
		if ( !body.IsValid() ) return;
		if ( body.IsProxy ) return;

		// We already launched.
		if ( _launched ) return;

		RemoveJoint();

		var mass = body.Mass;
		body.ApplyImpulse( force.Normal * (mass * force.Length) );
		body.PhysicsBody?.ApplyAngularImpulse( Vector3.Random * (mass * force.Length) );

		_launched = true;
	}

	static void GetConnectedBodies( GameObject source, HashSet<Rigidbody> result )
	{
		foreach ( var rb in source.Root.Components.GetAll<Rigidbody>() )
		{
			if ( !result.Add( rb ) ) continue;

			foreach ( var joint in rb.Joints )
			{
				if ( joint.Object1 != null ) GetConnectedBodies( joint.Object1, result );
				if ( joint.Object2 != null ) GetConnectedBodies( joint.Object2, result );
			}
		}
	}
}
