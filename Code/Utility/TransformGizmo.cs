/// <summary>
/// A move/rotate/scale gizmo. Draws and handles input, feed it the camera and mouse input with Update once a frame,
/// then call one of <see cref="Move"/>, <see cref="Rotate"/> or <see cref="Scale"/> with the current value and apply the new value it hands back.
/// </summary>
public sealed class TransformGizmo
{
	enum Kind
	{
		None,
		Arrow,
		Plane,
		ViewPlane,
		Ring,
		ViewRing,
		ScaleArrow,
		ScaleBox
	}

	readonly record struct Handle( Kind Kind, int Axis );

	// Appearance settings
	const float ArrowStart = 2.0f;
	const float ArrowLength = 24.0f;
	const float ArrowGirth = 6.0f;
	const float HeadLength = 4.0f;
	const float SquareHalfSize = 3.0f;
	const float SquareOffset = 9.0f;
	const float RingRadius = 19.0f;
	const float ViewRingRadius = 22.0f;
	const float CullAngle = 10.0f;
	const int RingSections = 64;

	// Editor drag rates, per world unit dragged
	const float DegreesPerUnit = 4.0f;
	const float ScalePerUnit = 0.01f;

	static readonly Color[] AxisColors = { "#ff1b58", "#88ff26", "#0051e8" };

	/// <summary>
	/// Default scale for the gizmo
	/// </summary>
	public float Size { get; set; } = 0.625f;

	public TransformGizmo()
	{
	}

	/// <param name="size">How big the handles are compared to the editor's gizmo, see <see cref="Size"/>.</param>
	public TransformGizmo( float size )
	{
		Size = size;
	}

	/// <summary>
	/// The cursor is over a handle, so a click right now would grab it.
	/// </summary>
	public bool IsHovered => _hovered.Kind != Kind.None;

	/// <summary>
	/// A handle is being dragged.
	/// </summary>
	public bool IsDragging => _active.Kind != Kind.None;

	CameraComponent _camera;
	Vector2 _cursor;
	Ray _ray;
	Ray _previousRay;
	bool _mouseDown;
	bool _pressed;

	Handle _hovered;
	Handle _active;
	Vector3 _ringTangent;

	readonly Vector3[] _ringPoints = new Vector3[RingSections + 1];

	/// <summary>
	/// Feed the camera and mouse in, once a frame before calling a control.
	/// </summary>
	public void Update( CameraComponent camera, Vector2 cursor, bool mouseDown )
	{
		_camera = camera;
		_cursor = cursor;
		_previousRay = _ray;
		_ray = camera.ScreenPixelToRay( cursor );

		_pressed = mouseDown && !_mouseDown;
		_mouseDown = mouseDown;

		if ( !mouseDown )
			_active = default;

		_hovered = default;
	}

	/// <summary>
	/// Let go of whatever is being dragged, for when input stops reaching the gizmo.
	/// </summary>
	public void Cancel()
	{
		_active = default;
		_hovered = default;
		_mouseDown = false;
		_pressed = false;
	}

	/// <summary>
	/// Arrows along <paramref name="axes"/>, squares to drag on two of them and a centre square to drag across the
	/// screen. Returns true with the new position while a handle is dragged.
	/// </summary>
	public bool Move( Vector3 position, Rotation axes, out Vector3 newPosition )
	{
		newPosition = position;
		if ( !_camera.IsValid() )
			return false;

		var unit = UnitScale( position );
		var pixels = PixelsPerUnit( position, unit );

		if ( !IsDragging )
		{
			var best = float.MaxValue;

			for ( int i = 0; i < 3; i++ )
			{
				var axis = Axis( axes, i );
				if ( IsCulled( axis ) ) continue;

				var distance = SegmentDistance( position + axis * (ArrowStart * unit), position + axis * (ArrowLength * unit) );
				if ( distance < MathF.Max( ArrowGirth * 0.5f * pixels, 5.0f ) && distance < best )
				{
					best = distance;
					_hovered = new Handle( Kind.Arrow, i );
				}
			}

			for ( int i = 0; i < 3; i++ )
			{
				if ( InsideSquare( position, axes, i, unit ) )
					_hovered = new Handle( Kind.Plane, i );
			}

			if ( _cursor.Distance( Screen( position ) ) < MathF.Max( SquareHalfSize * pixels, 5.0f ) )
				_hovered = new Handle( Kind.ViewPlane, 0 );
		}

		Press();

		using ( BeginDraw() )
		{
			for ( int i = 0; i < 3; i++ )
			{
				var axis = Axis( axes, i );
				if ( IsCulled( axis ) ) continue;

				SetColor( new Handle( Kind.Arrow, i ), AxisColors[i] );
				DrawArrow( position, axis, unit, cone: true );
			}

			for ( int i = 0; i < 3; i++ )
			{
				SetColor( new Handle( Kind.Plane, i ), AxisColors[SquareColor( i )] );
				DrawSquare( position, axes, i, unit );
			}

			SetColor( new Handle( Kind.ViewPlane, 0 ), Color.White );
			DrawViewSquare( position, unit );
		}

		if ( !IsDragging || !_mouseDown )
			return false;

		Vector3 movement;

		switch ( _active.Kind )
		{
			case Kind.Arrow:
				{
					var axis = Axis( axes, _active.Axis );
					movement = axis * MouseDelta( position, FacingPlaneNormal( axis ) ).Dot( axis );
					break;
				}

			case Kind.Plane:
				movement = MouseDelta( position, Axis( axes, _active.Axis ) );
				break;

			case Kind.ViewPlane:
				movement = MouseDelta( position, _camera.WorldRotation.Forward );
				break;

			default:
				return false;
		}

		if ( movement.IsNearlyZero() )
			return false;

		newPosition = position + movement;
		return true;
	}

	/// <summary>
	/// A ring around each of <paramref name="rotation"/>'s axes and a ring facing the camera. Returns true with the new rotation while a ring is dragged.
	/// </summary>
	public bool Rotate( Vector3 position, Rotation rotation, out Rotation newRotation )
	{
		newRotation = rotation;
		if ( !_camera.IsValid() )
			return false;

		var unit = UnitScale( position );
		var pixels = PixelsPerUnit( position, unit );
		var viewAxis = -_camera.WorldRotation.Forward;

		if ( !IsDragging )
		{
			var best = MathF.Max( 2.0f * pixels, 6.0f );

			for ( int i = 0; i < 3; i++ )
			{
				var distance = RingDistance( position, RingAxis( rotation, i ), RingRadius * unit, halfFacingCamera: true );
				if ( distance < best )
				{
					best = distance;
					_hovered = new Handle( Kind.Ring, i );
				}
			}

			var viewDistance = RingDistance( position, viewAxis, ViewRingRadius * unit, halfFacingCamera: false );
			if ( viewDistance < best )
				_hovered = new Handle( Kind.ViewRing, 0 );
		}

		if ( Press() && _active.Kind is Kind.Ring or Kind.ViewRing )
		{
			// The tangent of the ring where it was grabbed is the direction to drag along
			var axis = _active.Kind == Kind.ViewRing ? viewAxis : RingAxis( rotation, _active.Axis );
			var radial = RingGrabDirection( position, axis );
			_ringTangent = Vector3.Cross( axis, radial ).Normal;

			if ( Vector3.Dot( Rotation.FromAxis( axis, 1.0f ) * radial - radial, _ringTangent ) < 0 )
				_ringTangent = -_ringTangent;
		}

		using ( BeginDraw() )
		{
			for ( int i = 0; i < 3; i++ )
			{
				var handle = new Handle( Kind.Ring, i );
				SetColor( handle, AxisColors[i] );
				DrawRing( position, RingAxis( rotation, i ), RingRadius * unit, halfFacingCamera: _active != handle );
			}

			SetColor( new Handle( Kind.ViewRing, 0 ), Color.White );
			DrawRing( position, viewAxis, ViewRingRadius * unit, halfFacingCamera: false );
		}

		if ( !IsDragging || !_mouseDown || _active.Kind is not (Kind.Ring or Kind.ViewRing) )
			return false;

		var ringAxis = _active.Kind == Kind.ViewRing ? viewAxis : RingAxis( rotation, _active.Axis );

		// Drag across the camera, or on the ring's own plane when it faces the camera
		var dragNormal = MathF.Abs( _camera.WorldRotation.Forward.Dot( ringAxis ) ) > 0.5f ? ringAxis : _camera.WorldRotation.Forward;
		var angle = MouseDelta( position, dragNormal ).Dot( _ringTangent ) * DegreesPerUnit;

		if ( angle == 0.0f )
			return false;

		newRotation = (Rotation.FromAxis( ringAxis, angle ) * rotation).Normal;
		return true;
	}

	/// <summary>
	/// A box in the middle that changes a uniform scale: dragging it right or up grows, like the centre box of the
	/// editor's scale gizmo. Returns true with the new scale while it's dragged.
	/// </summary>
	public bool Scale( Vector3 position, Rotation axes, float scale, out float newScale )
	{
		newScale = scale;

		if ( !ScaleControl( position, axes, perAxis: false, out var change ) )
			return false;

		newScale = scale + change.x;
		return true;
	}

	/// <summary>
	/// Box-headed arrows that scale along each of <paramref name="axes"/>, and a box in the middle that scales all
	/// of them, like the editor's scale gizmo. The scale's x, y and z go with forward, left and up. Returns true with
	/// the new scale while a handle is dragged.
	/// </summary>
	public bool Scale( Vector3 position, Rotation axes, Vector3 scale, out Vector3 newScale )
	{
		newScale = scale;

		if ( !ScaleControl( position, axes, perAxis: true, out var change ) )
			return false;

		newScale = scale + change;
		return true;
	}

	/// <summary>
	/// The scale handles, giving how much to add to the scale this frame, per axis.
	/// </summary>
	bool ScaleControl( Vector3 position, Rotation axes, bool perAxis, out Vector3 change )
	{
		change = Vector3.Zero;
		if ( !_camera.IsValid() )
			return false;

		var unit = UnitScale( position );
		var pixels = PixelsPerUnit( position, unit );

		if ( !IsDragging )
		{
			var best = MathF.Max( ArrowGirth * 0.5f * pixels, 5.0f );

			if ( perAxis )
			{
				for ( int i = 0; i < 3; i++ )
				{
					var axis = Axis( axes, i );
					if ( IsCulled( axis ) ) continue;

					var distance = SegmentDistance( position + axis * (ArrowStart * unit), position + axis * (ArrowLength * unit) );
					if ( distance < best )
					{
						best = distance;
						_hovered = new Handle( Kind.ScaleArrow, i );
					}
				}
			}

			// The centre box sits in front of the arrows
			if ( _cursor.Distance( Screen( position ) ) < MathF.Max( SquareHalfSize * pixels, 6.0f ) )
				_hovered = new Handle( Kind.ScaleBox, 0 );
		}

		Press();

		using ( BeginDraw() )
		{
			if ( perAxis )
			{
				for ( int i = 0; i < 3; i++ )
				{
					var axis = Axis( axes, i );
					if ( IsCulled( axis ) ) continue;

					SetColor( new Handle( Kind.ScaleArrow, i ), AxisColors[i] );
					DrawArrow( position, axis, unit, cone: false );
				}
			}

			// The size of the move gizmo's centre square, it's the only handle when the scale is uniform
			SetColor( new Handle( Kind.ScaleBox, 0 ), Color.White );
			Gizmo.Transform = new Transform( position, axes );
			Gizmo.Draw.SolidBox( BBox.FromPositionAndSize( Vector3.Zero, SquareHalfSize * 2.0f * unit ) );
		}

		if ( !IsDragging || !_mouseDown )
			return false;

		switch ( _active.Kind )
		{
			case Kind.ScaleArrow:
				{
					var axis = Axis( axes, _active.Axis );
					var movement = MouseDelta( position, FacingPlaneNormal( axis ) ).Dot( axis ) * ScalePerUnit;
					change = _active.Axis switch
					{
						0 => new Vector3( movement, 0, 0 ),
						1 => new Vector3( 0, movement, 0 ),
						_ => new Vector3( 0, 0, movement )
					};
					break;
				}

			case Kind.ScaleBox:
				{
					// Dragging right or up grows, whichever way the gizmo faces
					var camera = _camera.WorldRotation;
					var movement = MouseDelta( position, camera.Forward ).Dot( camera.Right + camera.Up ) * ScalePerUnit;
					change = new Vector3( movement );
					break;
				}

			default:
				return false;
		}

		return !change.IsNearlyZero();
	}

	/// <summary>
	/// Grab the hovered handle on the frame the mouse goes down. True when something was grabbed.
	/// </summary>
	bool Press()
	{
		if ( !_pressed || IsDragging || !IsHovered )
			return false;

		_active = _hovered;
		_pressed = false;
		return true;
	}

	static Vector3 Axis( Rotation axes, int index ) => index switch
	{
		0 => axes.Forward,
		1 => axes.Left,
		_ => axes.Up
	};

	// Pitch turns about left, yaw about up and roll about forward, coloured like the editor's
	static Vector3 RingAxis( Rotation rotation, int index ) => index switch
	{
		0 => rotation.Left,
		1 => rotation.Up,
		_ => rotation.Forward
	};

	/// <summary>
	/// World size of one gizmo unit at this position, so the gizmo keeps its size on screen like the editor's.
	/// </summary>
	float UnitScale( Vector3 position )
	{
		if ( _camera.Orthographic )
			return Size * _camera.OrthographicHeight * 0.006f;

		var size = _camera.ScreenRect.Size;
		var screenScale = size.Length > 0 ? 1024.0f / size.Length : 1.0f;
		var distance = _camera.WorldPosition.Distance( position );

		// The editor's scale uses the horizontal field of view, convert it like the camera does for rendering
		var fieldOfView = _camera.FieldOfView;
		if ( _camera.FovAxis == CameraComponent.Axis.Vertical && size.y > 0 )
			fieldOfView = Sandbox.Screen.CreateVerticalFieldOfView( fieldOfView, size.x / size.y );

		return Size * screenScale * distance * fieldOfView.DegreeToRadian() * 0.006f;
	}

	float PixelsPerUnit( Vector3 position, float unit )
	{
		return Screen( position + _camera.WorldRotation.Right * unit ).Distance( Screen( position ) );
	}

	Vector2 Screen( Vector3 position ) => _camera.PointToScreenPixels( position );

	bool IsCulled( Vector3 axis )
	{
		var angle = Vector3.GetAngle( axis, _camera.WorldRotation.Forward );
		return angle < CullAngle || angle > 180.0f - CullAngle;
	}

	/// <summary>
	/// A plane through the axis, turned to face the camera as much as it can.
	/// </summary>
	Vector3 FacingPlaneNormal( Vector3 axis )
	{
		var forward = _camera.WorldRotation.Forward;
		var normal = forward - axis * forward.Dot( axis );
		return normal.IsNearlyZero() ? forward : normal.Normal;
	}

	/// <summary>
	/// How far the mouse moved this frame on a plane through <paramref name="point"/>, in world units.
	/// </summary>
	Vector3 MouseDelta( Vector3 point, Vector3 normal )
	{
		var plane = new Plane( point, normal );
		if ( !plane.TryTrace( _ray, out var now, true ) || !plane.TryTrace( _previousRay, out var before, true ) )
			return Vector3.Zero;

		return now - before;
	}

	Vector3 RingGrabDirection( Vector3 center, Vector3 axis )
	{
		if ( new Plane( center, axis ).TryTrace( _ray, out var hit, true ) && !(hit - center).IsNearlyZero() )
			return (hit - center).Normal;

		// Looking along the ring's plane, fall back to the direction across the screen
		var across = Vector3.Cross( axis, _camera.WorldRotation.Forward );
		return across.IsNearlyZero() ? Vector3.Cross( axis, Vector3.Up ).Normal : across.Normal;
	}

	float SegmentDistance( Vector3 a, Vector3 b )
	{
		var sa = Screen( a );
		var sb = Screen( b );
		var ab = sb - sa;
		var lengthSquared = ab.LengthSquared;
		var t = lengthSquared > 0 ? Math.Clamp( Vector2.Dot( _cursor - sa, ab ) / lengthSquared, 0.0f, 1.0f ) : 0.0f;
		return _cursor.Distance( sa + ab * t );
	}

	void FillRing( Vector3 center, Vector3 axis, float radius )
	{
		var basis = Rotation.LookAt( axis );

		for ( int i = 0; i <= RingSections; i++ )
		{
			var angle = i * MathF.Tau / RingSections;
			_ringPoints[i] = center + (basis.Left * MathF.Cos( angle ) + basis.Up * MathF.Sin( angle )) * radius;
		}
	}

	bool FacesCamera( Vector3 center, Vector3 point )
	{
		return Vector3.Dot( point - center, _camera.WorldPosition - center ) >= 0;
	}

	float RingDistance( Vector3 center, Vector3 axis, float radius, bool halfFacingCamera )
	{
		FillRing( center, axis, radius );

		var best = float.MaxValue;

		for ( int i = 0; i < RingSections; i++ )
		{
			if ( halfFacingCamera && !FacesCamera( center, _ringPoints[i] ) && !FacesCamera( center, _ringPoints[i + 1] ) )
				continue;

			best = MathF.Min( best, SegmentDistance( _ringPoints[i], _ringPoints[i + 1] ) );
		}

		return best;
	}

	bool InsideSquare( Vector3 position, Rotation axes, int normalAxis, float unit )
	{
		var (u, v) = SquareAxes( axes, normalAxis );
		var center = position + (u + v) * (SquareOffset * unit);
		var half = SquareHalfSize * unit;

		var a = Screen( center - u * half - v * half );
		var b = Screen( center + u * half - v * half );
		var c = Screen( center + u * half + v * half );
		var d = Screen( center - u * half + v * half );

		return InsideTriangle( _cursor, a, b, c ) || InsideTriangle( _cursor, a, c, d );
	}

	static bool InsideTriangle( Vector2 p, Vector2 a, Vector2 b, Vector2 c )
	{
		static float Side( Vector2 p, Vector2 a, Vector2 b ) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

		var d1 = Side( p, a, b );
		var d2 = Side( p, b, c );
		var d3 = Side( p, c, a );
		var negative = d1 < 0 || d2 < 0 || d3 < 0;
		var positive = d1 > 0 || d2 > 0 || d3 > 0;
		return !(negative && positive);
	}

	// The two axes a square lies on, given the axis it faces
	static (Vector3 U, Vector3 V) SquareAxes( Rotation axes, int normalAxis ) => normalAxis switch
	{
		0 => (axes.Left, axes.Up),
		1 => (axes.Forward, axes.Up),
		_ => (axes.Forward, axes.Left)
	};

	// Square colours as the editor has them: facing forward is blue, facing left is red, facing up is green
	static int SquareColor( int normalAxis ) => normalAxis switch
	{
		0 => 2,
		1 => 0,
		_ => 1
	};

	/// <summary>
	/// A gizmo scope in world space that draws over everything.
	/// </summary>
	static IDisposable BeginDraw()
	{
		var scope = Gizmo.Scope( "sandbox-transform-gizmo" );
		Gizmo.Transform = global::Transform.Zero;
		Gizmo.Draw.IgnoreDepth = true;
		return scope;
	}

	/// <summary>
	/// Darker until hovered, faded while another handle is dragged.
	/// </summary>
	void SetColor( Handle handle, Color color )
	{
		var thickness = MathF.Max( 3.0f * Size, 1.5f );

		if ( IsDragging && _active != handle )
		{
			color = color.Darken( 0.33f ).WithAlphaMultiplied( 0.5f );
			thickness *= 0.66f;
		}
		else if ( _active != handle && _hovered != handle )
		{
			color = color.Darken( 0.33f );
		}

		Gizmo.Draw.Color = color;
		Gizmo.Draw.LineThickness = thickness;
	}

	void DrawArrow( Vector3 position, Vector3 axis, float unit, bool cone )
	{
		var start = position + axis * (ArrowStart * unit);
		var headStart = position + axis * ((ArrowLength - HeadLength) * unit);

		Gizmo.Draw.Line( start, headStart );

		if ( cone )
			Gizmo.Draw.SolidCone( headStart, axis * (HeadLength * unit), HeadLength * 0.33f * unit );
		else
			Gizmo.Draw.SolidBox( BBox.FromPositionAndSize( headStart + axis * (HeadLength * 0.5f * unit), HeadLength * 0.5f * unit ) );
	}

	void DrawSquare( Vector3 position, Rotation axes, int normalAxis, float unit )
	{
		var (u, v) = SquareAxes( axes, normalAxis );
		var center = position + (u + v) * (SquareOffset * unit);
		var half = SquareHalfSize * unit;

		var a = center - u * half - v * half;
		var b = center + u * half - v * half;
		var c = center + u * half + v * half;
		var d = center - u * half + v * half;

		Gizmo.Draw.Line( a, b );
		Gizmo.Draw.Line( b, c );
		Gizmo.Draw.Line( c, d );
		Gizmo.Draw.Line( d, a );
	}

	void DrawViewSquare( Vector3 position, float unit )
	{
		var right = _camera.WorldRotation.Right * (SquareHalfSize * unit);
		var up = _camera.WorldRotation.Up * (SquareHalfSize * unit);

		Gizmo.Draw.Line( position - right - up, position + right - up );
		Gizmo.Draw.Line( position + right - up, position + right + up );
		Gizmo.Draw.Line( position + right + up, position - right + up );
		Gizmo.Draw.Line( position - right + up, position - right - up );
	}

	void DrawRing( Vector3 center, Vector3 axis, float radius, bool halfFacingCamera )
	{
		FillRing( center, axis, radius );

		for ( int i = 0; i < RingSections; i++ )
		{
			if ( halfFacingCamera && !FacesCamera( center, _ringPoints[i] ) && !FacesCamera( center, _ringPoints[i + 1] ) )
				continue;

			Gizmo.Draw.Line( _ringPoints[i], _ringPoints[i + 1] );
		}
	}
}
