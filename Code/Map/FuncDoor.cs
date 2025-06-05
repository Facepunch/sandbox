[EditorHandle( Icon = "touch_app" )]
public sealed class FuncDoor : BaseToggle, Component.IPressable
{
	public delegate Task ButtonDelegate( GameObject presser );

	[Property] public ButtonDelegate OnButtonPressed { get; set; }
	[Property] public ButtonDelegate OnButtonReleased { get; set; }

	[Property, Group( "Opening" ), Order( 1 )] public Action OnOpenStart { get; set; }
	[Property, Group( "Opening" ), Order( 1 )] public Action OnOpenEnd { get; set; }
	[Property, Group( "Opening" ), Order( 1 )] public float OpenDuration { get; set; } = 1.0f;
	[Property, Group( "Opening" ), Order( 1 )] public Curve OpenMovementCurve { get; set; } = new Curve( new Curve.Frame( 0, 0 ), new Curve.Frame( 1, 1 ) );

	[Property, Group( "Closing" ), Order( 2 )] public Action OnCloseStart { get; set; }
	[Property, Group( "Closing" )] public Action OnCloseEnd { get; set; }
	[Property, Group( "Closing" )] public float CloseDuration { get; set; } = 1.0f;
	[Property, Group( "Closing" )] public Curve CloseMovementCurve { get; set; } = new Curve( new Curve.Frame( 0, 0 ), new Curve.Frame( 1, 1 ) );

	[Property] public bool AutoReset { get; set; } = true;
	[Property, ShowIf( "AutoReset", true )] public float ResetTime { get; set; } = 1.0f;

	[Property, Group( "Movement" )] public Vector3 Pivot { get; set; }
	[Property, Group( "Movement" )] public Angles TargetRotation { get; set; } = new Angles( 0, 90, 0 );
	[Property, Group( "Movement" )] public bool OpenAwayFromPlayer { get; set; } = false;

	Transform initialTransform;

	[Sync] public bool IsMoving { get; set; }

	protected override void OnStart()
	{
		base.OnStart();

		initialTransform = LocalTransform;

		// initial state pos
		if ( State ) LocalTransform = initialTransform.RotateAround( initialTransform.PointToWorld( Pivot ), TargetRotation );
		else LocalTransform = initialTransform;
	}
	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		if ( !Gizmo.IsSelected )
			return;

		using ( Gizmo.Scope( "Tool", new Transform( Pivot ) ) )
		{
			Gizmo.Hitbox.DepthBias = 0.1f;

			if ( Gizmo.Control.Position( "pivot", 0, out var newPivot ) )
			{
				Pivot += newPivot;
			}
		}

		var delta = MathF.Sin( RealTime.Now * 2.0f ).Remap( -1, 1 );
		DrawAt( delta );
		DrawAt( 0 );
		DrawAt( 1 );
	}

	void DrawAt( float f )
	{
		Gizmo.Transform = WorldTransform.RotateAround( WorldTransform.PointToWorld( Pivot ), TargetRotation * f );

		var bbox = GameObject.GetLocalBounds();

		Gizmo.Draw.IgnoreDepth = false;
		Gizmo.Draw.Color = Color.Yellow;
		Gizmo.Draw.LineThickness = 3;
		Gizmo.Draw.LineBBox( bbox );
		Gizmo.Draw.IgnoreDepth = true;

		Gizmo.Draw.LineThickness = 1;
		Gizmo.Draw.Color = Gizmo.Draw.Color.WithAlpha( 0.3f );
		Gizmo.Draw.LineBBox( bbox );
	}

	[Rpc.Broadcast]
	public void Press( GameObject presser )
	{
		if ( presser.Network.Owner != Rpc.Caller )
			return;

		if ( IsMoving )
			return;

		OnButtonPressed?.Invoke( presser );

		if ( IsProxy )
			return;

		if ( State )
		{
			if ( !AutoReset ) Close();
			return;
		}

		Open( presser );
	}

	async void Open( GameObject presser )
	{
		OnOpenStart?.Invoke();
		IsMoving = true;

		var doorForward = LocalTransform.Rotation.Forward;
		var toPlayer = (presser.WorldPosition - LocalTransform.Position).Normal;

		var finalRotation = TargetRotation;

		bool isBehind = Vector3.Dot( toPlayer, doorForward ) > 0;

		if ( OpenAwayFromPlayer && isBehind )
		{
			finalRotation = new Angles( -TargetRotation.pitch, -TargetRotation.yaw, -TargetRotation.roll );
		}

		await AnimateRotationTo( finalRotation, OpenMovementCurve, OpenDuration );

		State = true;
		OnOpenEnd?.Invoke();

		if ( AutoReset && ResetTime >= 0.0f )
		{
			await Task.DelaySeconds( ResetTime );
			Close();
		}

		IsMoving = false;
	}

	async void Close()
	{
		OnCloseStart?.Invoke();

		IsMoving = true;

		await AnimateRotationTo( Rotation.Identity, CloseMovementCurve, CloseDuration );

		State = false;
		IsMoving = false;

		OnCloseEnd?.Invoke();
	}

	async Task AnimateRotationTo( Rotation rot, Curve curve, float time )
	{
		float d = 0;
		var start = LocalTransform;
		var localPivot = start.PointToWorld( Pivot );

		var targetTx = initialTransform.RotateAround( LocalTransform.PointToWorld( Pivot ), rot );

		var targetRot = initialTransform.Rotation * rot;

		//rot = rot * start.Rotation.Inverse;

		while ( time > d )
		{
			var delta = d.Remap( 0, time );

			var t = curve.Evaluate( delta );

			var rotDelta = Rotation.Lerp( start.Rotation, targetRot, t );

			var tx = start.RotateAround( localPivot, rotDelta * start.Rotation.Inverse );

			LocalTransform = tx;

			d += Time.Delta;

			await Task.FrameEnd();
		}

		LocalTransform = targetTx;
	}

	[Rpc.Broadcast]
	public void Release( GameObject presser )
	{
		if ( presser.Network.Owner != Rpc.Caller )
			return;

		OnButtonReleased?.Invoke( presser );

		if ( IsProxy )
			return;
	}


	[ActionGraphNode( "example.addone" ), Pure]
	[Title( "GetPlayer" ), Group( "Examples" ), Icon( "exposure_plus_1" )]
	public static Player GetPlayer( object value = default )
	{
		if ( value is GameObject o )
		{
			return o.Components.Get<Player>();
		}

		return null;
	}

	bool IPressable.Press( IPressable.Event e )
	{
		if ( IsMoving )
			return false;

		Press( e.Source.GameObject );
		return true;
	}

	void IPressable.Release( IPressable.Event e )
	{
		Release( e.Source.GameObject );
	}

	bool IPressable.CanPress( Sandbox.Component.IPressable.Event e )
	{
		if ( IsMoving ) return false;
		return true;
	}
}
