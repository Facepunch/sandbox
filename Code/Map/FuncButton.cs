[Alias( "Button" ), EditorHandle( Icon = "touch_app" )]
public sealed class FuncButton : BaseToggle, Component.IPressable
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

	[Property, Group( "Movement" ), Order( 0 )] public bool Move { get; set; }
	[Property, Group( "Movement" )] public Vector3 MoveDelta { get; set; }

	Vector3 initialPos;

	[Sync] public bool IsMoving { get; set; }

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		if ( !Gizmo.IsSelected )
			return;

		if ( !Move )
			return;

		Gizmo.Transform = WorldTransform;

		var bbox = GameObject.GetLocalBounds();
		bbox += MoveDelta * MathF.Sin( RealTime.Now * 2.0f ).Remap( -1, 1 );

		Gizmo.Draw.Color = Color.Yellow;
		Gizmo.Draw.LineThickness = 3;
		Gizmo.Draw.LineBBox( bbox );
		Gizmo.Draw.IgnoreDepth = true;

		Gizmo.Draw.LineThickness = 1;
		Gizmo.Draw.Color = Gizmo.Draw.Color.WithAlpha( 0.3f );
		Gizmo.Draw.LineBBox( bbox );
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		initialPos = LocalPosition;

		// initial state pos
		if ( State ) LocalPosition = initialPos + LocalRotation * MoveDelta;
		else LocalPosition = initialPos;
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

		Open();
	}

	async void Open()
	{
		OnOpenStart?.Invoke();
		IsMoving = true;

		await AnimatePositionTo( initialPos + LocalRotation * MoveDelta, OpenMovementCurve, OpenDuration );

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

		await AnimatePositionTo( initialPos, CloseMovementCurve, CloseDuration );

		State = false;
		IsMoving = false;

		OnCloseEnd?.Invoke();
	}

	async Task AnimatePositionTo( Vector3 pos, Curve curve, float time )
	{
		float d = 0;
		Vector3 start = LocalPosition;

		while ( time > d )
		{
			var delta = d.Remap( 0, time );

			LocalPosition = Vector3.Lerp( start, pos, curve.Evaluate( delta ) );

			d += Time.Delta;

			await Task.FrameEnd();
		}

		LocalPosition = pos;
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
