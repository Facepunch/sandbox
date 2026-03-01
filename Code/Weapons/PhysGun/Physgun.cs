using Sandbox.Physics;
using Sandbox.Rendering;

public partial class Physgun
{
	[Property, RequireComponent] public HighlightOutline BeamHighlight { get; set; }

	[Property, Group( "Sound" )] SoundEvent ReleasedSound { get; set; }
	[Property, Group( "Sound" )] SoundEvent ButtonInSound { get; set; }
	[Property, Group( "Sound" )] SoundEvent ButtonOutSound { get; set; }

	protected override string ScreenMaterialName => "v_physgun_display";
	protected override string ScreenMaterialPath => "weapons/physgun/physgun-screen.vmat";

	protected override Vector2Int ScreenTextureSize => new Vector2Int( 80, 80 );


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
	}

	[Sync] public GrabState _state { get; set; } = default;

	public GrabState _stateHovered { get; set; } = default;

	bool _preventReselect = false;

	bool _isSpinning;
	bool _isSnapping;
	Rotation _spinRotation;
	Rotation _snapRotation;

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
			var muzzle = WeaponModel?.MuzzleTransform?.WorldTransform ?? WorldTransform;
			UpdateBeam( muzzle, _state.EndPoint, _stateHovered.EndNormal, _state.IsValid() );
		}
		else
		{
			CloseBeam();
		}
	}

	private const int GraphSamples = 256;
	private float[] _graph1 = new float[GraphSamples];
	private float[] _graph2 = new float[GraphSamples];
	private float[] _graph3 = new float[GraphSamples];
	private int _graphCursor;
	private float _graphTimer;
	private const float GraphInterval = 0.01f;

	private float _plotValue1;
	private float _plotValue2;
	private float _plotValue3;

	private Texture _graphTexture;
	private byte[] _graphPixels = new byte[GraphSamples * 4]; // RGBA8

	protected override void DrawScreenContent( Rect rect, HudPainter paint )
	{
		paint.SetBlendMode( BlendMode.Lighten );

		var w = rect.Width;
		var h = rect.Height;

		var rowHeight = h * 0.25f;
		var padding = w * 0.03f;

		var row1Y = rect.Top + h * 0.125f;
		var barW = w * 0.55f;
		var barH = h * 0.1f;
		var barX = rect.Left + padding * 2f;
		var barY = row1Y + (rowHeight - barH) * 0.2f;
		var borderColor = new Color( 0.5f, 0.5f, 0.5f );

		var fillWidth = (barW - w * 0.03f) * MathF.Max( _plotValue1, _plotValue2 );
		if ( fillWidth > 0f )
		{
			paint.DrawRect( new Rect( barX + w * 0.02f, barY + h * 0.02f, fillWidth, barH - h * 0.03f ), new Color( 1, 1, 1, 0.8f ) );
		}

		// Bar outline
		paint.DrawLine( new Vector2( barX, barY ), new Vector2( barX + barW, barY ), 1f, borderColor );
		paint.DrawLine( new Vector2( barX, barY + barH ), new Vector2( barX + barW, barY + barH ), 1f, borderColor );
		paint.DrawLine( new Vector2( barX, barY ), new Vector2( barX, barY + barH ), 1f, borderColor );
		paint.DrawLine( new Vector2( barX + barW, barY ), new Vector2( barX + barW, barY + barH ), 1f, borderColor );

		var percentLabel = new TextRendering.Scope( "100", Color.White, h * 0.135f );
		percentLabel.FontName = "Consolas";
		percentLabel.TextColor = Color.White;
		percentLabel.FontWeight = 100;
		percentLabel.FilterMode = FilterMode.Point;
		paint.DrawText( percentLabel, new Rect( rect.Left + barW + padding * 4f, barY * 0.6f, w * 0.4f - padding * 2f, rowHeight ), TextFlag.LeftCenter );

		var row2Y = row1Y + rowHeight + h * -0.1f;

		var ch2 = new TextRendering.Scope( "Ch2", Color.White, h * 0.14f );
		ch2.FontName = "Consolas";
		ch2.TextColor = new Color( 0f, 1f, 0f );
		ch2.FontWeight = 400;
		ch2.FilterMode = FilterMode.Point;
		paint.DrawText( ch2, new Rect( rect.Left + padding, row2Y, w * 0.45f, rowHeight ), TextFlag.LeftCenter );

		var voltage = new TextRendering.Scope( "731v", Color.White, h * 0.14f );
		voltage.FontName = "Consolas";
		voltage.TextColor = new Color( 0f, 1f, 0f );
		voltage.FontWeight = 400;
		voltage.FilterMode = FilterMode.Point;
		paint.DrawText( voltage, new Rect( rect.Left + padding + w * 0.45f, row2Y, w * 0.45f, rowHeight ), TextFlag.LeftCenter );
	}

	private void UpdateScreenGraph()
	{
		var active1 = _state.Active && !_state.Pulling;
		var active2 = Input.Down( "attack2" ) && !_preventReselect || _state.Pulling;
		var active3 = _isSpinning;

		var target1 = active1 ? 0.8f + Random.Shared.Float( -0.05f, 0.05f ) : 0.1f + Random.Shared.Float( -0.02f, 0.02f );
		var target2 = active2 ? 0.8f + Random.Shared.Float( -0.05f, 0.05f ) : 0.1f + Random.Shared.Float( -0.02f, 0.02f );
		var target3 = active3 ? 0.8f + Random.Shared.Float( -0.3f, 0.3f ) : 0.1f + Random.Shared.Float( -0.02f, 0.02f );
		_plotValue1 = _plotValue1.LerpTo( target1, Time.Delta * 10f );
		_plotValue2 = _plotValue2.LerpTo( target2, Time.Delta * 10f );
		_plotValue3 = _plotValue3.LerpTo( target3, Time.Delta * 10f );

		_graphTimer += Time.Delta;
		while ( _graphTimer >= GraphInterval )
		{
			_graphTimer -= GraphInterval;
			_graph1[_graphCursor % GraphSamples] = _plotValue1;
			_graph2[_graphCursor % GraphSamples] = _plotValue2;
			_graph3[_graphCursor % GraphSamples] = _plotValue3;
			_graphCursor++;
		}

		var count = Math.Min( _graphCursor, GraphSamples );
		for ( var i = 0; i < GraphSamples; i++ )
		{
			float r, g, b;
			if ( i < count )
			{
				var idx = (_graphCursor - 1 - i + GraphSamples) % GraphSamples;
				r = _graph1[idx];
				g = _graph2[idx];
				b = _graph3[idx];
			}
			else
			{
				r = 0.1f;
				g = 0.1f;
				b = 0.1f;
			}

			var offset = i * 4;
			_graphPixels[offset + 0] = (byte)(r * 255f);
			_graphPixels[offset + 1] = (byte)(g * 255f);
			_graphPixels[offset + 2] = (byte)(b * 255f);
			_graphPixels[offset + 3] = 255;
		}

		_graphTexture ??= Texture.Create( GraphSamples, 1 ).WithDynamicUsage().Finish();
		_graphTexture.Update( _graphPixels );

		if ( !ViewModel.IsValid() ) return;

		var renderer = ViewModel.GetComponentInChildren<SkinnedModelRenderer>();
		if ( !renderer.IsValid() ) return;

		var so = renderer.SceneObject;
		so.Attributes.Set( "GraphData", _graphTexture );

		so.Attributes.Set( "Grid", new Vector4( 8f, 6f, 0.3f, 0f ) );
		so.Attributes.Set( "GraphInfo", new Vector4( GraphSamples, 0f, 0f, 0f ) );
		so.Attributes.Set( "Ch1Color", new Vector4( 0f, 1f, 1f, 1f ) );
		so.Attributes.Set( "Ch2Color", new Vector4( 1f, 1f, 0f, 1f ) );
		so.Attributes.Set( "Ch3Color", new Vector4( 1f, 0f, 0f, 0.5f ) );
		so.Attributes.Set( "Band1", new Vector4( 0.5f, 0.3f, 0f, 0f ) );
		so.Attributes.Set( "Band2", new Vector4( 0.48f, 0.28f, 0f, 0f ) );
		so.Attributes.Set( "Band3", new Vector4( 0.52f, 0.32f, 0f, 0f ) );
	}

	public override void OnControl( Player player )
	{
		base.OnControl( player );

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
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( !CanMove( _state ) )
		{
			RemoveJoint();

			if ( CanMove( _stateHovered ) && _stateHovered.Pulling )
			{
				var force = Owner.EyeTransform.Rotation.Backward * _stateHovered.Body.Mass * PullForce;
				_stateHovered.Body.ApplyForceAt( _stateHovered.EndPoint, force );
			}

			return;
		}

		_body ??= new PhysicsBody( Scene.PhysicsWorld ) { BodyType = PhysicsBodyType.Keyframed, AutoSleep = false };

		var eyeTransform = Owner.EyeTransform;
		var grabDistance = ClampGrabDistance( _state.Body, _state.EndPoint, eyeTransform, _state.GrabDistance );
		var targetPosition = eyeTransform.Position + eyeTransform.Rotation.Forward * grabDistance;
		var targetRotation = _state.Pulling
			? eyeTransform.Rotation * _state.GrabOffset
			: Rotation.FromYaw( Owner.Controller.EyeAngles.yaw ) * _state.GrabOffset;
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

	bool CanMove( GrabState state )
	{
		var player = Owner;
		if ( player is null ) return false;

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

		var tr = Scene.Trace.Ray( aim.Position, aim.Position + aim.Forward * 1000 )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.Run();

		state.LocalOffset = tr.EndPosition;
		state.LocalNormal = tr.Normal;
		state.Pulling = isPulling;

		if ( !tr.Hit || tr.Body is null ) return false;
		if ( tr.Component is not Rigidbody ) return false;

		var go = tr.Body.GameObject;
		if ( !go.IsValid() || go.IsDestroyed ) return false;

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

		var mass = body.Mass;
		body.ApplyImpulse( force.Normal * (mass * force.Length) );
		body.PhysicsBody?.ApplyAngularImpulse( Vector3.Random * (mass * force.Length) );
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
