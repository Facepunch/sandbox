public partial class Physgun : BaseCarryable
{
	float MovementSmoothness => 3;

	[Property, RequireComponent] public HighlightOutline BeamHighlight { get; set; }

	public struct GrabState
	{
		public GameObject GameObject { get; set; }
		public PhysicsBody Body { get; set; }
		public Vector3 LocalOffset { get; set; }
		public Vector3 LocalNormal { get; set; }
		public Transform GrabOffset { get; set; }
		public Vector3 EndPoint
		{
			get
			{
				if ( !GameObject.IsValid() ) return LocalOffset;
				return GameObject.WorldTransform.PointToWorld( LocalOffset );
			}
		}

		public Vector3 EndNormal
		{
			get
			{
				if ( !GameObject.IsValid() ) return LocalNormal;
				return GameObject.WorldTransform.NormalToWorld( LocalNormal );
			}
		}

		public bool IsValid() => Body.IsValid();
	}

	GrabState _state = default;
	GrabState _stateHovered = default;

	bool _preventReselect = false;

	bool _isSpinning;

	public override void OnCameraMove( Player player, ref Angles angles )
	{

		base.OnCameraMove( player, ref angles );

		if ( _state.IsValid() && _isSpinning )
		{
			angles = default;
		}
	}

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		_isSpinning = Input.Down( "use" );

		if ( _state.IsValid() )
		{
			OnControllingBody( player );
			return;
		}

		if ( _preventReselect )
		{
			if ( !Input.Down( "attack1" ) )
				_preventReselect = false;

			return;
		}


		bool validGrab = FindGrabbedBody( out _stateHovered, player.EyeTransform );

		if ( Input.Down( "attack1" ) )
		{
			var muzzle = WeaponModel?.MuzzleTransform?.WorldTransform ?? player.EyeTransform;

			//DebugOverlay.Line( muzzle.Position, _stateHovered.EndPoint, Color.Cyan );

			_state = _stateHovered;

			if ( _state.IsValid() )
			{
				_state.Body.MotionEnabled = true;
			}

			UpdateBeam( muzzle, _stateHovered.EndPoint, _stateHovered.EndNormal );
		}
		else
		{
			_preventReselect = false;
			CloseBeam();
		}
	}

	void OnControllingBody( Player player )
	{
		var muzzle = WeaponModel?.MuzzleTransform?.WorldTransform ?? player.EyeTransform;
		//DebugOverlay.Line( muzzle.Position, _state.EndPoint, Color.Cyan );

		UpdateBeam( muzzle, _state.EndPoint, _stateHovered.EndNormal );

		if ( FreeCamGameObjectSystem.Current.IsActive )
			return;

		if ( Input.Down( "attack2" ) )
		{
			_state.GrabOffset = player.EyeTransform.ToLocal( _state.Body.Transform );

			// TODO - this should add or update a Component 
			// on the GameObject so we can undo it etc
			_state.Body.MotionEnabled = false;
			_state.Body.Velocity = 0;
			_state.Body.AngularVelocity = 0;

			_state = default;

			_preventReselect = true;
			return;
		}


		if ( _isSpinning )
		{
			var go = _state.GrabOffset;
			go.Rotation = (Input.AnalogLook * -1) * go.Rotation;
			_state.GrabOffset = go;
		}

		if ( !Input.Down( "attack1" ) )
		{
			_state = default;
			return;
		}
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		var player = Owner;
		if ( !player.IsLocalPlayer ) return;

		if ( _state.IsValid() )
		{
			var targetTx = player.EyeTransform.ToWorld( _state.GrabOffset );
			_state.Body.SmoothMove( targetTx, 0.02f * MovementSmoothness, Time.Delta );
			return;
		}
	}

	bool FindGrabbedBody( out GrabState state, Transform aim )
	{
		state = default;

		var tr = Scene.Trace.Ray( aim.Position, aim.Position + aim.Forward * 1000 )
				.IgnoreGameObjectHierarchy( GameObject.Root )
				.Run();

		state.LocalOffset = tr.EndPosition;
		state.LocalNormal = tr.Normal;

		if ( !tr.Hit || tr.Body is null ) return false;
		if ( tr.Body.BodyType == PhysicsBodyType.Static ) return false;
		if ( tr.Body.BodyType == PhysicsBodyType.Keyframed ) return false;

		state.Body = tr.Body;
		state.GameObject = tr.Body.GameObject;
		state.LocalOffset = state.GameObject.WorldTransform.PointToLocal( tr.HitPosition );
		state.LocalNormal = state.GameObject.WorldTransform.NormalToLocal( tr.Normal );
		state.GrabOffset = aim.ToLocal( tr.Body.Transform );
		return true;
	}


}
