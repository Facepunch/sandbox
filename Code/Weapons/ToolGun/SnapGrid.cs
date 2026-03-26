/// <summary>
/// Manages a world-space snap grid overlay projected onto a surface plane.
/// </summary>
public sealed class SnapGrid
{
	public float CellSize { get; set; }
	public float MaskRadius { get; set; }

	/// <summary>
	/// Fallback half-extent used when the hovered object has no usable bounds.
	/// </summary>
	public float GridSize { get; set; }

	[ConVar( "snap_grid_no_fade", ConVarFlags.Cheat )]
	private static bool NoFade { get; set; } = false;

	public SnapGrid( float cellSize = 4f, float maskRadius = 64f, float gridSize = 48f )
	{
		CellSize = cellSize;
		MaskRadius = maskRadius;
		GridSize = gridSize;
	}

	private sealed class SnapGridSceneObject : SceneDynamicObject
	{
		public SnapGridSceneObject( SceneWorld world ) : base( world )
		{
			Transform = Transform.Zero;
			Flags.IsOpaque = false;
			Flags.IsTranslucent = true;
			Flags.CastShadows = false;
			RenderLayer = SceneRenderLayer.OverlayWithDepth;
		}

		public void Write( Vector3 faceOrigin, Vector3 faceRight, Vector3 faceUp, Vector2 faceHalfExtents, Vector3 aimPos, float maskRadius, float cellSize )
		{
			var halfW = faceHalfExtents.x;
			var halfH = faceHalfExtents.y;

			// The quad is always centred at the projected bounds centre.
			var quadCenter = faceOrigin;

			var v00 = quadCenter - faceRight * halfW - faceUp * halfH;
			var v10 = quadCenter + faceRight * halfW - faceUp * halfH;
			var v11 = quadCenter + faceRight * halfW + faceUp * halfH;
			var v01 = quadCenter - faceRight * halfW + faceUp * halfH;

			Bounds = BBox.FromPositionAndSize( quadCenter, MathF.Max( halfW, halfH ) * 2f );

			Span<Vertex> verts = stackalloc Vertex[6]
			{
				new Vertex( v00 ),
				new Vertex( v10 ),
				new Vertex( v11 ),
				new Vertex( v00 ),
				new Vertex( v11 ),
				new Vertex( v01 ),
			};

			Init( Graphics.PrimitiveType.Triangles );
			AddVertex( verts );
		}
	}

	private SnapGridSceneObject _sceneObj;
	private Material _material;

	private Vector3 _cachedOrigin;
	private Vector3 _cachedNormal;
	private Vector3 _cachedRight;
	private Vector3 _cachedUp;
	private Vector2 _cachedHalfExtents;
	private GameObject _cachedObject;
	private bool _hasPlane;

	/// <summary>
	/// The snapped world-space position of the highlighted corner
	/// </summary>
	public Vector3 LastSnapWorldPos { get; private set; }

	/// <summary>
	/// Returns the nearest grid corner index (cx, cy) and its world-space position,
	/// given the face plane in world space and the aim world position.
	/// </summary>
	public static (int cx, int cy, Vector3 snapPos, float snapU, float snapV, bool snapAxisX, bool snapAxisY) ComputeSnap(
		Vector3 faceOriginWs,
		Vector3 faceRightWs,
		Vector3 faceUpWs,
		float cellSize,
		Vector3 aimPosWs )
	{
		var offset = aimPosWs - faceOriginWs;
		var u = Vector3.Dot( offset, faceRightWs );
		var v = Vector3.Dot( offset, faceUpWs );

		var cx = (int)MathF.Round( u / cellSize );
		var cy = (int)MathF.Round( v / cellSize );

		var distX = MathF.Abs( u - cx * cellSize );
		var distY = MathF.Abs( v - cy * cellSize );

		float snapU, snapV;
		bool snapAxisX, snapAxisY;

		// Near a corner: snap to intersection and show both lines.
		if ( distX < cellSize * 0.35f && distY < cellSize * 0.35f )
		{
			snapU = cx * cellSize;
			snapV = cy * cellSize;
			snapAxisX = true;
			snapAxisY = true;
		}
		else if ( distX <= distY )
		{
			snapU = cx * cellSize;
			snapV = v;
			snapAxisX = true;
			snapAxisY = false;
		}
		else
		{
			snapU = u;
			snapV = cy * cellSize;
			snapAxisX = false;
			snapAxisY = true;
		}

		var snapPos = faceOriginWs + faceRightWs * snapU + faceUpWs * snapV;
		return (cx, cy, snapPos, snapU, snapV, snapAxisX, snapAxisY);
	}

	/// <summary>
	/// Called every frame when the weld tool hovers a valid object.
	/// After calling, read <see cref="LastSnapWorldPos"/> for a snapped position
	/// </summary>
	public void Update( SceneWorld world, GameObject hoveredObject, Vector3 aimWorldPos, Vector3 hitNormalWorld )
	{
		if ( !_sceneObj.IsValid() )
		{
			_material ??= Material.FromShader( "shaders/snap_grid.shader" );
			_sceneObj = new SnapGridSceneObject( world ) { Material = _material };
		}

		// Only recalculate the plane when the surface normal or hovered object changes
		var faceNormal = hitNormalWorld.Normal;
		var holdingUse = Input.Down( "use" );
		var objectChanged = hoveredObject != _cachedObject;
		var planeChanged = !_hasPlane || (objectChanged) || (!holdingUse && Vector3.Dot( faceNormal, _cachedNormal ) < 0.999f);

		if ( planeChanged )
		{
			_cachedNormal = faceNormal;
			_cachedObject = hoveredObject;

			var (obbCenter, obbHalfExtents, obbRotation, obbValid) = GetObjectOBB( hoveredObject );

			if ( obbValid )
			{
				// Align the grid so its "up" direction matches the object's world-space up
				// projected onto the face plane. This orients grid lines with the object's
				// natural rotation on every face, including tilted surfaces.
				var objUp = obbRotation * Vector3.Up;
				var dotUp = Vector3.Dot( objUp, faceNormal );

				if ( MathF.Abs( dotUp ) < 0.99f )
				{
					// Project the object's up onto the face plane and use it as the grid up.
					_cachedUp    = (objUp - faceNormal * dotUp).Normal;
					_cachedRight = Vector3.Cross( faceNormal, _cachedUp ).Normal;
				}
				else
				{
					// Degenerate: object's up is nearly parallel to the face normal
					// (e.g., hovering the top/bottom face of an upright object).
					// Fall back to the OBB axis most parallel to the face plane.
					var a0 = obbRotation * new Vector3( 1, 0, 0 );
					var a1 = obbRotation * new Vector3( 0, 1, 0 );
					var a2 = obbRotation * new Vector3( 0, 0, 1 );
					var d0 = MathF.Abs( Vector3.Dot( a0, faceNormal ) );
					var d1 = MathF.Abs( Vector3.Dot( a1, faceNormal ) );
					var d2 = MathF.Abs( Vector3.Dot( a2, faceNormal ) );
					var refAxis = d0 <= d1 && d0 <= d2 ? a0 : d1 <= d2 ? a1 : a2;
					_cachedRight = (refAxis - faceNormal * Vector3.Dot( refAxis, faceNormal )).Normal;
					_cachedUp    = Vector3.Cross( _cachedRight, faceNormal ).Normal;
				}

				_cachedOrigin = ProjectOntoPlane( obbCenter, aimWorldPos, faceNormal );
				_cachedHalfExtents = ProjectedHalfExtents( obbHalfExtents, obbRotation, _cachedRight, _cachedUp );
			}
			else
			{
				// Fallback: generic tangent for world geometry.
				var refAxis = MathF.Abs( Vector3.Dot( faceNormal, Vector3.Up ) ) > 0.9f
					? Vector3.Forward
					: Vector3.Up;
				_cachedRight = Vector3.Cross( faceNormal, refAxis ).Normal;
				_cachedUp = Vector3.Cross( _cachedRight, faceNormal ).Normal;
				_cachedOrigin = aimWorldPos;
				_cachedHalfExtents = new Vector2( GridSize, GridSize );
			}

			_hasPlane = true;
		}

		var halfExtents = _cachedHalfExtents;

		// Scale down cell size so at least one cell fits within the object's face.
		var cellSize = CellSize;
		var minHalf = MathF.Min( halfExtents.x, halfExtents.y );
		while ( minHalf < cellSize && cellSize > 0.1f )
			cellSize *= 0.5f;

		// Compute the nearest snap line
		var (cx, cy, snapPos, snapU, snapV, snapAxisX, snapAxisY) = ComputeSnap( _cachedOrigin, _cachedRight, _cachedUp, cellSize, aimWorldPos );
		LastSnapWorldPos = snapPos;

		_sceneObj.Write( _cachedOrigin, _cachedRight, _cachedUp, halfExtents, aimWorldPos, MaskRadius, cellSize );

		_sceneObj.Attributes.Set( "GridOrigin", _cachedOrigin );
		_sceneObj.Attributes.Set( "GridRight", _cachedRight );
		_sceneObj.Attributes.Set( "GridUp", _cachedUp );
		_sceneObj.Attributes.Set( "AimPoint", aimWorldPos );
		_sceneObj.Attributes.Set( "MaskRadius", NoFade ? float.MaxValue : MaskRadius );
		_sceneObj.Attributes.Set( "HalfExtents", halfExtents );
		_sceneObj.Attributes.Set( "CellSize", cellSize );
		_sceneObj.Attributes.Set( "SnapCornerX", snapU / cellSize );
		_sceneObj.Attributes.Set( "SnapCornerY", snapV / cellSize );
		_sceneObj.Attributes.Set( "SnapAxisX", snapAxisX ? 1.0f : 0.0f );
		_sceneObj.Attributes.Set( "SnapAxisY", snapAxisY ? 1.0f : 0.0f );
	}

	/// <summary>
	/// Hide the overlay
	/// </summary>
	public void Hide()
	{
		if ( _sceneObj != null && _sceneObj.IsValid() )
			_sceneObj.RenderingEnabled = false;
		_hasPlane = false;
	}

	// ---------------------------------------------------------------------------
	// Helpers

	/// <summary>
	/// Returns the OBB (oriented bounding box) of <paramref name="go"/> in world space.
	/// Prefers model-local bounds (tight, rotation-aware) over collider world AABB.
	/// </summary>
	private static (Vector3 Center, Vector3 HalfExtents, Rotation Rotation, bool Valid) GetObjectOBB( GameObject go )
	{
		var worldTx = go.WorldTransform;
		var rot = worldTx.Rotation;
		var scale = worldTx.Scale;

		// Try model-local bounds first — these are tight and unaffected by world rotation.
		BBox? localBox = null;
		var mr = go.GetComponentInChildren<ModelRenderer>( false );
		if ( mr != null && mr.Model != null )
			localBox = mr.Model.Bounds;

		if ( localBox == null )
		{
			var smr = go.GetComponentInChildren<SkinnedModelRenderer>( false );
			if ( smr != null && smr.Model != null )
				localBox = smr.Model.Bounds;
		}

		if ( localBox != null )
		{
			var lb = localBox.Value;
			if ( lb.Size.Length > 0.1f )
			{
				var center = worldTx.PointToWorld( lb.Center );
				var halfExtents = lb.Size * 0.5f * scale;
				return (center, halfExtents, rot, true);
			}
		}

		// Fallback: world AABB from colliders, treated as an identity-rotation OBB.
		var colliders = go.GetComponentsInChildren<Collider>( false, true ).ToArray();
		if ( colliders.Length > 0 )
		{
			var box = colliders[0].GetWorldBounds();
			for ( int i = 1; i < colliders.Length; i++ )
				box = box.AddBBox( colliders[i].GetWorldBounds() );

			if ( box.Size.Length > 0.1f )
				return (box.Center, box.Size * 0.5f, Rotation.Identity, true);
		}

		return (Vector3.Zero, Vector3.Zero, Rotation.Identity, false);
	}

	/// <summary>
	/// Projects <paramref name="point"/> onto the plane defined by <paramref name="planePoint"/> and <paramref name="normal"/>.
	/// </summary>
	private static Vector3 ProjectOntoPlane( Vector3 point, Vector3 planePoint, Vector3 normal )
	{
		return point - normal * Vector3.Dot( point - planePoint, normal );
	}

	/// <summary>
	/// Returns the half-extents of an OBB projected along <paramref name="right"/> and <paramref name="up"/>.
	/// Uses the OBB support function, which correctly handles rotated objects.
	/// </summary>
	private static Vector2 ProjectedHalfExtents( Vector3 obbHalfExtents, Rotation obbRotation, Vector3 right, Vector3 up )
	{
		// Explicitly rotate the three local unit axes into world space to avoid
		// ambiguity with named helpers (Right/Up/Forward vary by coordinate convention).
		var ax = obbRotation * new Vector3( 1, 0, 0 );
		var ay = obbRotation * new Vector3( 0, 1, 0 );
		var az = obbRotation * new Vector3( 0, 0, 1 );
		var e = obbHalfExtents;

		var halfRight = MathF.Abs( e.x * Vector3.Dot( ax, right ) )
					  + MathF.Abs( e.y * Vector3.Dot( ay, right ) )
					  + MathF.Abs( e.z * Vector3.Dot( az, right ) );

		var halfUp = MathF.Abs( e.x * Vector3.Dot( ax, up ) )
				   + MathF.Abs( e.y * Vector3.Dot( ay, up ) )
				   + MathF.Abs( e.z * Vector3.Dot( az, up ) );

		return new Vector2( halfRight, halfUp );
	}

	/// <summary>
	/// Destroys the underlying scene object.
	/// </summary>
	public void Destroy()
	{
		_sceneObj?.Delete();
		_sceneObj = null;
	}
}
