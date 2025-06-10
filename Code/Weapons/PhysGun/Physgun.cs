public class Physgun : BaseCarryable
{
	float MovementSmoothness => 7;

	public struct GrabState
	{
		public GameObject GameObject { get; set; }
		public PhysicsBody Body { get; set; }
		public Vector3 LocalOffset { get; set; }
		public Transform GrabOffset { get; set; }
		public Vector3 EndPoint
		{
			get
			{
				if ( !GameObject.IsValid() ) return LocalOffset;
				return GameObject.WorldTransform.PointToWorld( LocalOffset );
			}
		}

		public bool IsValid() => Body.IsValid();
	}

	GrabState _state = default;

	bool _preventReselect = false;

	public override void OnControl( Player player )
	{
		base.OnControl( player );

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


		bool validGrab = FindGrabbedBody( out var grabState, player.EyeTransform );

		if ( Input.Down( "attack1" ) )
		{
			var muzzle = WeaponModel?.MuzzleTransform?.WorldTransform ?? player.EyeTransform;

			DebugOverlay.Line( muzzle.Position, grabState.EndPoint, Color.Cyan );


			_state = grabState;

			if ( _state.IsValid() )
			{
				_state.Body.MotionEnabled = true;
			}
		}
		else
		{
			_preventReselect = false;
		}
	}

	void OnControllingBody( Player player )
	{
		var muzzle = WeaponModel?.MuzzleTransform?.WorldTransform ?? player.EyeTransform;
		DebugOverlay.Line( muzzle.Position, _state.EndPoint, Color.Cyan );

		if ( FreeCamGameObjectSystem.Current.IsActive )
			return;

		if ( Input.Down( "attack2" ) )
		{
			// TODO - this should add or update a Component 
			// on the GameObject so we can undo it etc
			_state.Body.MotionEnabled = false;
			_state.Body.Velocity = 0;
			_state.Body.AngularVelocity = 0;

			_state = default;

			_preventReselect = true;
			return;
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

		if ( !tr.Hit || tr.Body is null ) return false;
		if ( tr.Body.BodyType == PhysicsBodyType.Static ) return false;
		if ( tr.Body.BodyType == PhysicsBodyType.Keyframed ) return false;

		state.Body = tr.Body;
		state.GameObject = tr.Body.GameObject;
		state.LocalOffset = state.GameObject.WorldTransform.PointToLocal( tr.HitPosition );
		state.GrabOffset = aim.ToLocal( tr.Body.Transform );
		return true;
	}


}
