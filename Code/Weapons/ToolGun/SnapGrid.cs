/// <summary>
/// Manages a world-space snap grid overlay projected onto a surface plane.
/// </summary>
public sealed class SnapGrid
{
	public float CellSize { get; set; }
	public float MaskRadius { get; set; }
	public float GridSize { get; set; }

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
			// slightly larger than the visible mask
			var halfW = MathF.Min( faceHalfExtents.x, maskRadius + cellSize );
			var halfH = MathF.Min( faceHalfExtents.y, maskRadius + cellSize );

			var aimOffset = aimPos - faceOrigin;
			var cx = Vector3.Dot( aimOffset, faceRight );
			var cy = Vector3.Dot( aimOffset, faceUp );

			// Clamp quad center so the quad stays within the face.
			cx = Math.Clamp( cx, -faceHalfExtents.x + halfW, faceHalfExtents.x - halfW );
			cy = Math.Clamp( cy, -faceHalfExtents.y + halfH, faceHalfExtents.y - halfH );

			var quadCenter = faceOrigin + faceRight * cx + faceUp * cy;

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
	private bool _hasPlane;

	/// <summary>
	/// The snapped world-space position of the highlighted corner
	/// </summary>
	public Vector3 LastSnapWorldPos { get; private set; }

	/// <summary>
	/// Returns the nearest grid corner index (cx, cy) and its world-space position,
	/// given the face plane in world space and the aim world position.
	/// </summary>
	public static (int cx, int cy, Vector3 snapPos) ComputeSnap(
		Vector3 faceOriginWs,
		Vector3 faceRightWs,
		Vector3 faceUpWs,
		float cellSize,
		Vector3 aimPosWs )
	{
		var offset = aimPosWs - faceOriginWs;
		var u = Vector3.Dot( offset, faceRightWs );
		var v = Vector3.Dot( offset, faceUpWs );

		// Snap to visual grid corners (half-cell offset)
		var cx = (int)MathF.Round( u / cellSize - 0.5f );
		var cy = (int)MathF.Round( v / cellSize - 0.5f );

		var snapPos = faceOriginWs
			+ faceRightWs * ((cx + 0.5f) * cellSize)
			+ faceUpWs * ((cy + 0.5f) * cellSize);

		return (cx, cy, snapPos);
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

		// Only recalculate the plane when the surface normal changes
		var faceNormal = hitNormalWorld.Normal;
		var holdingUse = Input.Down( "use" );
		var planeChanged = !_hasPlane || (!holdingUse && Vector3.Dot( faceNormal, _cachedNormal ) < 0.999f);

		if ( planeChanged )
		{
			var refAxis = MathF.Abs( Vector3.Dot( faceNormal, Vector3.Up ) ) > 0.9f
				? Vector3.Forward
				: Vector3.Up;
			_cachedRight = Vector3.Cross( faceNormal, refAxis ).Normal;
			_cachedUp = Vector3.Cross( _cachedRight, faceNormal ).Normal;
			_cachedNormal = faceNormal;
			_cachedOrigin = aimWorldPos;
			_hasPlane = true;
		}

		var halfExtents = new Vector2( GridSize, GridSize );

		// Compute the nearest snap corner
		var (cx, cy, snapPos) = ComputeSnap( _cachedOrigin, _cachedRight, _cachedUp, CellSize, aimWorldPos );
		LastSnapWorldPos = snapPos;

		_sceneObj.Write( _cachedOrigin, _cachedRight, _cachedUp, halfExtents, aimWorldPos, MaskRadius, CellSize );

		_sceneObj.Attributes.Set( "GridOrigin", _cachedOrigin );
		_sceneObj.Attributes.Set( "GridRight", _cachedRight );
		_sceneObj.Attributes.Set( "GridUp", _cachedUp );
		_sceneObj.Attributes.Set( "AimPoint", aimWorldPos );
		_sceneObj.Attributes.Set( "MaskRadius", MaskRadius );
		_sceneObj.Attributes.Set( "CellSize", CellSize );
		_sceneObj.Attributes.Set( "SnapCornerX", (float)cx );
		_sceneObj.Attributes.Set( "SnapCornerY", (float)cy );
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

	/// <summary>
	/// Destroys the underlying scene object.
	/// </summary>
	public void Destroy()
	{
		_sceneObj?.Delete();
		_sceneObj = null;
	}
}
