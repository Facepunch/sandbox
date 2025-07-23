public partial class Physgun : BaseCarryable
{
	float MovementSmoothness => 3;

	[Property, RequireComponent] public HighlightOutline BeamHighlight { get; set; }

	public struct GrabState
	{
		public GameObject GameObject { get; set; }
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

		public bool IsValid() => GameObject.IsValid();

		public Rigidbody Body => GameObject?.GetComponent<Rigidbody>();
	}

	[Sync]
	public GrabState _state { get; set; } = default;

	public GrabState _stateHovered { get; set; } = default;

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

	protected override void OnPreRender()
	{
		base.OnPreRender();

		var player = GetComponentInParent<Player>();


		if ( _state.IsValid() )
		{
			var muzzle = WeaponModel?.MuzzleTransform?.WorldTransform ?? WorldTransform;
			UpdateBeam( muzzle, _state.EndPoint, _stateHovered.EndNormal );
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

		_isSpinning = Input.Down( "use" );

		if ( _state.IsValid() )
		{
			if ( !Input.Down( "attack1" ) )
			{
				_state = default;
				_preventReselect = true;
				return;
			}

			if ( Input.Down( "attack2" ) )
			{
				Freeze( _state.Body );
				_state = default;
				_preventReselect = true;
				return;
			}

			if ( !Input.MouseWheel.IsNearZeroLength )
			{
				var state = _state;
				var go = state.GrabOffset;

				var targetDistance = go.Position.Length + Input.MouseWheel.y * 20.0f;

				if ( targetDistance > 40 )
				{
					go.Position = targetDistance * go.Position.Normal;
					state.GrabOffset = go;

					// State needs to reset for sync to detect a change, bug or how it's meant to work?
					_state = default;
					_state = state;
				}

				// stop processing this so inventory doesn't change
				Input.MouseWheel = default;
			}


			if ( _isSpinning )
			{
				var state = _state;
				var go = state.GrabOffset;

				go.Position += state.LocalOffset * go.Rotation;
				go.Rotation = (Input.AnalogLook * -1) * go.Rotation;
				go.Position -= state.LocalOffset * go.Rotation;

				state.GrabOffset = go;

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
		bool validGrab = FindGrabbedBody( out sh, player.EyeTransform );
		_stateHovered = sh;

		if ( Input.Down( "attack1" ) )
		{
			var muzzle = WeaponModel?.MuzzleTransform?.WorldTransform ?? player.EyeTransform;

			_state = _stateHovered;

			if ( _state.IsValid() )
			{
				Unfreeze( _state.Body );
			}
		}
		else
		{
			_preventReselect = false;
		}
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		var player = Owner;
		if ( player is null ) return;

		if ( !_state.IsValid() ) return;
		if ( !_state.Body.IsValid() ) return;

		// Only move the body if we own it.
		if ( _state.Body.IsProxy ) return;

		// Only move the body if it's dynamic.
		if ( !_state.Body.MotionEnabled ) return;

		var targetTx = player.EyeTransform.ToWorld( _state.GrabOffset );
		_state.Body.SmoothMove( targetTx, 0.02f * MovementSmoothness, Time.Delta );
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
		if ( tr.Component is not Rigidbody ) return false;

		var go = tr.Body.GameObject;
		if ( !go.IsValid() ) return false;

		// Trace hits physics, convert to local using scaled physics transform.
		var bodyTransform = tr.Body.Transform.WithScale( go.WorldScale );

		state.GameObject = go;
		state.LocalOffset = bodyTransform.PointToLocal( tr.HitPosition );
		state.LocalNormal = bodyTransform.NormalToLocal( tr.Normal );
		state.GrabOffset = aim.ToLocal( bodyTransform );
		return true;
	}

	[Rpc.Host]
	void Freeze( Rigidbody body )
	{
		if ( !body.IsValid() ) return;
		if ( body.IsProxy ) return;

		body.MotionEnabled = false;
	}

	[Rpc.Host]
	void Unfreeze( Rigidbody body )
	{
		if ( !body.IsValid() ) return;
		if ( body.IsProxy ) return;

		body.MotionEnabled = true;
	}
}
