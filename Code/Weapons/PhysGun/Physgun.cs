using Sandbox.Physics;

public partial class Physgun : BaseCarryable
{
	[Property, RequireComponent] public HighlightOutline BeamHighlight { get; set; }

	[Property, Group( "Sound" )] SoundEvent ReleasedSound { get; set; }
	[Property, Group( "Sound" )] SoundEvent ButtonInSound { get; set; }
	[Property, Group( "Sound" )] SoundEvent ButtonOutSound { get; set; }

	public struct GrabState
	{
		public bool Active { get; set; }
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
	}

	[Sync]
	public GrabState _state { get; set; } = default;

	public GrabState _stateHovered { get; set; } = default;

	bool _preventReselect = false;

	bool _isSpinning;
	bool _isSnapping;
	Rotation _spinRotation;
	Rotation _snapRotation;

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

		if ( _state.Active )
		{
			var muzzle = WeaponModel?.MuzzleTransform?.WorldTransform ?? WorldTransform;
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

		ViewModel?.RunEvent<ViewModel>( UpdateViewModel );

		if ( _state.IsValid() )
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
					var eyeRotation = Rotation.FromYaw( player.Controller.EyeAngles.yaw );

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

		if ( _preventReselect )
		{
			if ( !Input.Down( "attack1" ) )
				_preventReselect = false;

			return;
		}

		var sh = _stateHovered;
		bool validGrab = FindGrabbedBody( out sh, player.EyeTransform, player.Controller.EyeAngles.yaw );
		_stateHovered = sh;

		if ( Input.Down( "attack1" ) )
		{
			ViewModel?.RunEvent<ViewModel>( x => x.OnAttack() );

			var muzzle = WeaponModel?.MuzzleTransform?.WorldTransform ?? player.EyeTransform;

			_state = _stateHovered with { Active = true };

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
		model.Renderer?.Set( "brake", _state.Active ? 1 : 0 );
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
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( !CanMove() )
		{
			RemoveJoint();

			return;
		}

		_body ??= new PhysicsBody( Scene.PhysicsWorld )
		{
			BodyType = PhysicsBodyType.Keyframed,
			AutoSleep = false
		};

		var eyeTransform = Owner.EyeTransform;
		var targetPosition = eyeTransform.Position + eyeTransform.Rotation.Forward * _state.GrabDistance;
		var targetRotation = Rotation.FromYaw( Owner.Controller.EyeAngles.yaw ) * _state.GrabOffset;
		_body.Transform = new Transform( targetPosition, targetRotation );

		if ( _joint is null )
		{
			var body = _state.Body.PhysicsBody;
			var point1 = new PhysicsPoint( _body );
			var point2 = new PhysicsPoint( body, _state.LocalOffset );
			var maxForce = body.Mass * body.World.Gravity.LengthSquared;

			_joint = PhysicsJoint.CreateControl( point1, point2 );
			_joint.LinearSpring = new PhysicsSpring( 32, 4, maxForce );
			_joint.AngularSpring = new PhysicsSpring( 64, 4, maxForce * 3 );
		}
	}

	bool CanMove()
	{
		var player = Owner;
		if ( player is null ) return false;

		if ( !_state.IsValid() ) return false;
		if ( !_state.Body.IsValid() ) return false;

		// Only move the body if we own it.
		if ( _state.Body.IsProxy ) return false;

		// Only move the body if it's dynamic.
		if ( !_state.Body.MotionEnabled ) return false;
		if ( !_state.Body.PhysicsBody.IsValid() ) return false;

		return true;
	}

	bool FindGrabbedBody( out GrabState state, Transform aim, float yaw )
	{
		state = default;

		var tr = Scene.Trace.Ray( aim.Position, aim.Position + aim.Forward * 1000 )
				.IgnoreGameObjectHierarchy( GameObject.Root )
				.Run();

		state.LocalOffset = tr.EndPosition;
		state.LocalNormal = tr.Normal;

		if ( !tr.Hit || tr.Body is null ) return false;
		if ( tr.Component is not Rigidbody ) return false;

		var go = tr.Body.GameObject;
		if ( !go.IsValid() ) return false;

		// Trace hits physics, convert to local using scaled physics transform.
		var bodyTransform = tr.Body.Transform.WithScale( go.WorldScale );

		state.GameObject = go;
		state.LocalOffset = bodyTransform.PointToLocal( tr.HitPosition );
		state.LocalNormal = bodyTransform.NormalToLocal( tr.Normal );
		state.GrabOffset = Rotation.FromYaw( yaw ).Inverse * bodyTransform.Rotation;
		state.GrabDistance = Vector3.DistanceBetween( aim.Position, tr.HitPosition );
		state.GrabDistance = MathF.Max( 0.0f, state.GrabDistance );

		_spinRotation = state.GrabOffset;
		_snapRotation = _spinRotation;

		return true;
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
