using Sandbox.Rendering;

/// <summary>
/// Draws the <see cref="DecalStamp"/>s on one object. Stamps that overlap on the same surface plane are baked on the GPU
/// into one set of textures and drawn as a single decal. It's local to each client, they all bake their own from the synced stamps.
/// New stamps show on their own straight away and merge once things settle.
/// </summary>
public sealed class DecalMerger : Component
{
	/// <summary>
	/// Merging waits until nothing has changed for this long, like when a spray ends
	/// </summary>
	const float SettleTime = 0.3f;

	/// <summary>
	/// Largest side of a baked texture, big merges lose resolution past this
	/// </summary>
	const int MaxTextureSize = 2048;

	// how far apart and tilted stamps can be and still count as the same surface
	const float PlaneDistance = 2f;
	const float PlaneDot = 0.99f;

	static readonly Vector3[] Corners = [new( 0, -0.5f, -0.5f ), new( 0, 0.5f, -0.5f ), new( 0, -0.5f, 0.5f ), new( 0, 0.5f, 0.5f )];

	static ComputeShader _bakeShader;
	static ComputeShader BakeShader => _bakeShader ??= new( "shaders/decal_merge_cs.shader" );

	record struct Renderer( GameObject GameObject, Texture[] Textures, Guid[] Stamps );

	readonly List<Renderer> _renderers = [];
	readonly List<(CommandList Commands, CameraComponent Camera, TimeUntil Remove)> _bakes = [];
	bool _dirty;
	TimeSince _sinceChange;

	/// <summary>
	/// The merger for the stamps under <paramref name="parent"/>, made when the first one arrives
	/// </summary>
	public static DecalMerger For( GameObject parent )
	{
		var merger = Find( parent );
		if ( merger.IsValid() ) return merger;

		var go = new GameObject( false, "decal merger" );
		go.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked | GameObjectFlags.Hidden;
		go.NetworkMode = NetworkMode.Never;
		go.SetParent( parent, false );
		merger = go.AddComponent<DecalMerger>();
		go.Enabled = true;
		return merger;
	}

	public static DecalMerger Find( GameObject parent ) => parent?.Children.Select( x => x.GetComponent<DecalMerger>() ).FirstOrDefault( x => x.IsValid() );

	public void MarkDirty()
	{
		_dirty = true;
		_sinceChange = 0;
	}

	protected override void OnUpdate()
	{
		// a bake runs every frame it's attached, it clears first so that's harmless, leaving it a moment makes sure it ran
		foreach ( var bake in _bakes.Where( x => x.Remove ).ToList() )
		{
			bake.Camera?.RemoveCommandList( bake.Commands );
			_bakes.Remove( bake );
		}

		if ( !_dirty ) return;

		var stamps = GameObject.Parent.Children
			.Select( x => x.GetComponent<DecalStamp>() )
			.Where( x => x.IsValid() && x.Active && x.Definition is not null )
			.ToList();

		if ( stamps.Count == 0 )
		{
			GameObject.Destroy();
			return;
		}

		// show new stamps straight away, drop what was removed
		var ids = stamps.Select( x => x.Id ).ToHashSet();

		foreach ( var renderer in _renderers.Where( x => !x.Stamps.All( ids.Contains ) ).ToList() )
			Remove( renderer );

		var shown = _renderers.SelectMany( x => x.Stamps ).ToHashSet();

		foreach ( var stamp in stamps.Where( x => !shown.Contains( x.Id ) ) )
			_renderers.Add( CreateSingle( stamp ) );

		if ( _sinceChange < SettleTime ) return;
		_dirty = false;

		// nothing renders on a dedicated server, the stamps on their own are fine
		var camera = Scene.Camera;
		if ( Application.IsDedicatedServer || !camera.IsValid() ) return;

		foreach ( var group in Cluster( stamps ) )
		{
			if ( group.Count < 2 ) continue;

			// already drawn as exactly this group
			var members = group.Select( x => x.Id ).ToHashSet();
			var existing = _renderers.Where( x => x.Stamps.Any( members.Contains ) ).ToList();
			if ( existing.Count == 1 && existing[0].Stamps.Length == members.Count ) continue;

			existing.ForEach( Remove );
			_renderers.Add( CreateMerged( group, camera ) );
		}
	}

	protected override void OnDestroy()
	{
		foreach ( var bake in _bakes )
			bake.Camera?.RemoveCommandList( bake.Commands );

		foreach ( var renderer in _renderers.ToList() )
			Remove( renderer );
	}

	void Remove( Renderer renderer )
	{
		_renderers.Remove( renderer );
		if ( renderer.GameObject.IsValid() ) renderer.GameObject.Destroy();

		foreach ( var texture in renderer.Textures )
			texture?.Dispose();
	}

	/// <summary>
	/// Stamps merge when they'd draw the same way, the same texture channels, colour mix and attenuation.
	/// Emissive, animated sprite sheet and non alpha blended decals stay on their own.
	/// </summary>
	static bool Compatible( DecalStamp a, DecalStamp b )
	{
		var x = a.Definition;
		var y = b.Definition;

		return x.ColorTexture.IsValid() && y.ColorTexture.IsValid()
			&& x.BlendMode == DecalBlendMode.Alpha && y.BlendMode == DecalBlendMode.Alpha
			&& !x.EmissiveTexture.IsValid() && !y.EmissiveTexture.IsValid()
			&& !x.ColorTexture.HasAnimatedSequences && !y.ColorTexture.HasAnimatedSequences
			&& x.NormalTexture.IsValid() == y.NormalTexture.IsValid()
			&& x.RoughMetalOcclusionTexture.IsValid() == y.RoughMetalOcclusionTexture.IsValid()
			&& x.HeightTexture.IsValid() == y.HeightTexture.IsValid()
			&& x.ColorMix.AlmostEqual( y.ColorMix )
			&& a.Attenuation.AlmostEqual( b.Attenuation );
	}

	// ponytail: O(n²) per object, a spatial hash if surfaces collect thousands of stamps
	static IEnumerable<List<DecalStamp>> Cluster( List<DecalStamp> stamps )
	{
		var root = Enumerable.Range( 0, stamps.Count ).ToArray();
		int Find( int i ) => root[i] == i ? i : root[i] = Find( root[i] );

		for ( var i = 0; i < stamps.Count; i++ )
			for ( var j = i + 1; j < stamps.Count; j++ )
				if ( Touches( stamps[i], stamps[j] ) ) root[Find( i )] = Find( j );

		return Enumerable.Range( 0, stamps.Count ).GroupBy( Find ).Select( x => x.Select( i => stamps[i] )
			.OrderBy( s => s.Layer ).ThenBy( s => s.Id ).ToList() );
	}

	static bool Touches( DecalStamp a, DecalStamp b )
	{
		if ( !Compatible( a, b ) ) return false;

		var normal = a.LocalRotation.Forward;
		if ( normal.Dot( b.LocalRotation.Forward ) < PlaneDot ) return false;

		var offset = b.LocalPosition - a.LocalPosition;
		if ( MathF.Abs( offset.Dot( normal ) ) > PlaneDistance ) return false;

		// bounding circles, cheaper than an exact overlap and merging near misses is harmless
		return offset.Length <= Radius( a ) + Radius( b );
	}

	static float Radius( DecalStamp stamp ) => stamp.Box.Scale.WithX( 0 ).Length * 0.5f;

	Decal CreateDecal( string name, Transform transform )
	{
		var go = new GameObject( false, name );
		go.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked | GameObjectFlags.Hidden;
		go.NetworkMode = NetworkMode.Never;
		go.SetParent( GameObject, false );
		go.LocalTransform = transform;
		return go.AddComponent<Decal>();
	}

	Renderer CreateSingle( DecalStamp stamp )
	{
		var decal = CreateDecal( "decal", stamp.LocalTransform );
		decal.Decals = [stamp.Definition];
		decal.ColorTint = stamp.Tint;
		decal.Scale = stamp.Scale;
		decal.Rotation = stamp.Rotation;
		decal.AttenuationAngle = stamp.Attenuation;
		decal.SortLayer = stamp.Layer;
		decal.GameObject.Enabled = true;

		return new Renderer( decal.GameObject, [], [stamp.Id] );
	}

	/// <summary>
	/// One decal covering every stamp in the group, its textures baked by a command list that runs on <paramref name="camera"/>
	/// before the opaque pass, so it's ready the frame it first draws
	/// </summary>
	Renderer CreateMerged( List<DecalStamp> stamps, CameraComponent camera )
	{
		// faces the way the first stamp does, grown on its plane to fit all of them
		var source = stamps[0].Definition;
		var frame = stamps[0].LocalTransform;
		var min = new Vector2( float.MaxValue );
		var max = new Vector2( float.MinValue );
		var density = 0f;

		foreach ( var stamp in stamps )
		{
			var box = stamp.Box;
			foreach ( var corner in Corners )
			{
				var p = frame.PointToLocal( box.PointToWorld( corner ) );
				min = Vector2.Min( min, new Vector2( p.y, p.z ) );
				max = Vector2.Max( max, new Vector2( p.y, p.z ) );
			}

			// texels per unit, the sharpest stamp sets the resolution
			density = MathF.Max( density, stamp.Definition.ColorTexture.Width / (stamp.Definition.Width * stamp.Scale) );
		}

		var size = max - min;
		density = MathF.Min( density, MaxTextureSize / MathF.Max( size.x, size.y ) );
		var width = Math.Max( 1, (int)MathF.Ceiling( size.x * density ) );
		var height = Math.Max( 1, (int)MathF.Ceiling( size.y * density ) );

		var center = (min + max) * 0.5f;
		var transform = new Transform( frame.PointToWorld( new Vector3( 0, center.x, center.y ) ), frame.Rotation, frame.Scale );
		var target = transform.WithScale( transform.Scale * new Vector3( DecalStamp.Depth, size.x, size.y ) );

		// floating point and linear, the shader writes the values the decal shader reads
		Texture Canvas( bool used ) => used ? Texture.Create( width, height, ImageFormat.RGBA16161616F ).WithUAVBinding().WithMips().Finish() : null;

		var color = Canvas( true );
		var normal = Canvas( source.NormalTexture.IsValid() );
		var rmo = Canvas( source.RoughMetalOcclusionTexture.IsValid() );
		var heightMap = Canvas( source.HeightTexture.IsValid() );
		var parallax = source.HeightTexture.IsValid() ? stamps.Max( x => x.Definition.ParallaxStrength ) : 0;

		var commands = new CommandList( "DecalMerge" );
		commands.Clear( color, Color.Transparent );
		if ( normal is not null ) commands.Clear( normal, new Color( 0.5f, 0.5f, 1f ) );
		if ( rmo is not null ) commands.Clear( rmo, Color.Black );
		if ( heightMap is not null ) commands.Clear( heightMap, Color.White );

		var a = commands.Attributes;
		a.Set( "ColorOut", color );
		if ( normal is not null ) a.Set( "NormalOut", normal );
		if ( rmo is not null ) a.Set( "RmoOut", rmo );
		if ( heightMap is not null ) a.Set( "HeightOut", heightMap );
		a.Set( "HasNormal", normal is null ? 0 : 1 );
		a.Set( "HasRmo", rmo is null ? 0 : 1 );
		a.Set( "HasHeight", heightMap is null ? 0 : 1 );

		var targetBasis = TangentBasis( target.Rotation );

		foreach ( var stamp in stamps )
		{
			var def = stamp.Definition;
			var box = stamp.Box;

			// the decal shader samples at uv = 0.5 - local.yz, in both decals
			Vector2 StampUv( float x, float y )
			{
				var local = box.PointToLocal( target.PointToWorld( new Vector3( 0, 0.5f - x / width, 0.5f - y / height ) ) );
				return new Vector2( 0.5f - local.y, 0.5f - local.z );
			}

			// only the texels under this stamp
			var lo = new Vector2( float.MaxValue );
			var hi = new Vector2( float.MinValue );
			foreach ( var corner in Corners )
			{
				var p = target.PointToLocal( box.PointToWorld( corner ) );
				var texel = new Vector2( (0.5f - p.y) * width, (0.5f - p.z) * height );
				lo = Vector2.Min( lo, texel );
				hi = Vector2.Max( hi, texel );
			}

			var x0 = Math.Max( 0, (int)lo.x );
			var y0 = Math.Max( 0, (int)lo.y );
			var x1 = Math.Min( width, (int)MathF.Ceiling( hi.x ) );
			var y1 = Math.Min( height, (int)MathF.Ceiling( hi.y ) );
			if ( x1 <= x0 || y1 <= y0 ) continue;

			var origin = StampUv( 0, 0 );
			var basis = TangentBasis( box.Rotation );

			a.Set( "Color", def.ColorTexture );
			if ( normal is not null ) a.Set( "Normal", def.NormalTexture );
			if ( rmo is not null ) a.Set( "Rmo", def.RoughMetalOcclusionTexture );
			if ( heightMap is not null ) a.Set( "Height", def.HeightTexture );
			a.Set( "Offset", new Vector2( x0, y0 ) );
			a.Set( "Size", new Vector2( x1 - x0, y1 - y0 ) );
			a.Set( "Origin", origin );
			a.Set( "StepX", StampUv( 1, 0 ) - origin );
			a.Set( "StepY", StampUv( 0, 1 ) - origin );
			a.Set( "Tint", def.Tint * stamp.Tint );
			a.Set( "BasisX", new Vector3( targetBasis.x.Dot( basis.x ), targetBasis.x.Dot( basis.y ), targetBasis.x.Dot( basis.z ) ) );
			a.Set( "BasisY", new Vector3( targetBasis.y.Dot( basis.x ), targetBasis.y.Dot( basis.y ), targetBasis.y.Dot( basis.z ) ) );
			a.Set( "BasisZ", new Vector3( targetBasis.z.Dot( basis.x ), targetBasis.z.Dot( basis.y ), targetBasis.z.Dot( basis.z ) ) );
			a.Set( "CoverageAmount", def.CoverageAmount );
			a.Set( "DepthScale", parallax > 0 ? def.ParallaxStrength / parallax : 0 );

			commands.DispatchCompute( BakeShader, x1 - x0, y1 - y0, 1 );
		}

		Texture[] textures = [color, normal, rmo, heightMap];
		foreach ( var texture in textures.Where( x => x is not null ) )
			commands.GenerateMipMaps( texture, Graphics.DownsampleMethod.Box );

		camera.AddCommandList( commands, Stage.AfterDepthPrepass );
		_bakes.Add( (commands, camera, 0.5f) );

		// ponytail: ColorMix below 1 mixes by ColorMix * merged alpha, a little weaker than separate decals where they overlap
		var decal = CreateDecal( "merged decal", transform );
		decal.Decals = [new DecalDefinition
		{
			ColorTexture = color,
			NormalTexture = normal,
			RoughMetalOcclusionTexture = rmo,
			HeightTexture = heightMap,
			ParallaxStrength = parallax,
			ColorMix = source.ColorMix,
			FilterMode = source.FilterMode,
			Width = size.x,
			Height = size.y,
		}];
		decal.Rotation = 0;
		decal.AttenuationAngle = stamps[0].Attenuation;
		decal.SortLayer = stamps[^1].Layer;
		decal.GameObject.Enabled = true;

		return new Renderer( decal.GameObject, textures, stamps.Select( x => x.Id ).ToArray() );
	}

	/// <summary>
	/// Where a decal's tangent space normal points, per <c>Decals.hlsl</c>: TransformNormal with -forward as the normal,
	/// -left and -up as tangents, and the tangent y flipped
	/// </summary>
	static (Vector3 x, Vector3 y, Vector3 z) TangentBasis( Rotation rotation ) => (-rotation.Left, rotation.Up, -rotation.Forward);
}
