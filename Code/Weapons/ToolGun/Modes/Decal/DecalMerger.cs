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

	/// <summary>
	/// A decal drawing some stamps. A merged one stays disabled while <see cref="Bake"/> runs on the GPU.
	/// </summary>
	record Renderer( GameObject GameObject, Guid[] Stamps, Texture[] Textures = null, CommandList Bake = null, CameraComponent Camera = null, uint Frame = 0 );

	readonly List<Renderer> _renderers = [];
	bool _dirty;
	TimeSince _sinceChange;

	/// <summary>
	/// The merger for the stamps under <paramref name="parent"/>, made when the first one arrives
	/// </summary>
	public static DecalMerger For( GameObject parent )
	{
		var merger = Find( parent );
		if ( merger.IsValid() ) return merger;

		merger = CreateChild( parent, "decal merger" ).AddComponent<DecalMerger>();
		merger.GameObject.Enabled = true;
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
		// ponytail: a GPU frame finishing after the bake was added means it ran, assumes the main camera renders every frame
		foreach ( var baked in _renderers.Where( x => !x.GameObject.Enabled && PerformanceStats.GpuFrameNumber > x.Frame + 1 ).ToList() )
		{
			baked.Camera.RemoveCommandList( baked.Bake );
			foreach ( var old in _renderers.Where( x => x != baked && x.Stamps.Any( baked.Stamps.Contains ) ).ToList() )
				Remove( old );

			baked.GameObject.Enabled = true;
		}

		if ( !_dirty ) return;

		var stamps = GameObject.Parent.Children
			.Select( x => x.GetComponent<DecalStamp>() )
			.Where( x => x.IsValid() && x.Active && x.Definition is not null )
			.ToList();

		// show new stamps straight away, drop what was removed
		var ids = stamps.Select( x => x.Id ).ToHashSet();

		foreach ( var renderer in _renderers.Where( x => !x.Stamps.All( ids.Contains ) ).ToList() )
			Remove( renderer );

		var shown = _renderers.Where( x => x.GameObject.Enabled ).SelectMany( x => x.Stamps ).ToHashSet();

		foreach ( var stamp in stamps.Where( x => !shown.Contains( x.Id ) ) )
			_renderers.Add( CreateSingle( stamp ) );

		var camera = Scene.Camera;
		if ( _sinceChange < SettleTime || !camera.IsValid() ) return;
		_dirty = false;

		// the order everything is drawn in right now, a merged decal's stamps in the order they were baked.
		// Baking in it means swapping to the merged decal doesn't change what's on top
		var drawn = new Dictionary<Guid, (uint Order, int Index)>();
		foreach ( var renderer in _renderers.Where( x => x.GameObject.Enabled ) )
			for ( var i = 0; i < renderer.Stamps.Length; i++ )
				drawn[renderer.Stamps[i]] = (DrawOrder( renderer.GameObject.GetComponent<Decal>() ), i);

		foreach ( var group in Cluster( stamps ) )
		{
			// already drawn or baking as exactly this group
			if ( group.Count < 2 || _renderers.Any( x => x.Stamps.Length == group.Count && group.All( s => x.Stamps.Contains( s.Id ) ) ) ) continue;

			group.Sort( ( a, b ) => drawn[a.Id].CompareTo( drawn[b.Id] ) );
			_renderers.Add( CreateMerged( group, camera ) );
		}
	}

	protected override void OnDestroy()
	{
		foreach ( var renderer in _renderers.ToList() )
			Remove( renderer );
	}

	void Remove( Renderer renderer )
	{
		_renderers.Remove( renderer );
		renderer.Camera?.RemoveCommandList( renderer.Bake );
		if ( renderer.GameObject.IsValid() ) renderer.GameObject.Destroy();

		foreach ( var texture in renderer.Textures ?? [] )
			texture.Dispose();
	}

	/// <summary>
	/// Stamps merge when they'd draw the same way, the same texture channels, filtering, colour mix and attenuation.
	/// Emissive, animated sprite sheet, point filtered and non alpha blended decals stay on their own.
	/// </summary>
	static bool Compatible( DecalStamp a, DecalStamp b )
	{
		var x = a.Definition;
		var y = b.Definition;

		return x.ColorTexture.IsValid() && y.ColorTexture.IsValid()
			&& x.BlendMode == DecalBlendMode.Alpha && y.BlendMode == DecalBlendMode.Alpha
			&& !x.EmissiveTexture.IsValid() && !y.EmissiveTexture.IsValid()
			&& !x.ColorTexture.HasAnimatedSequences && !y.ColorTexture.HasAnimatedSequences
			&& x.FilterMode != FilterMode.Point && x.FilterMode == y.FilterMode
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

		return Enumerable.Range( 0, stamps.Count ).GroupBy( Find ).Select( x => x.Select( i => stamps[i] ).ToList() );
	}

	/// <summary>
	/// The order the decal shader applies a decal in, later on top. Mirrors <c>Decal.UpdateSortLayer</c>:
	/// 8 bits of sort layer, then 24 bits of the GameObject id
	/// </summary>
	static uint DrawOrder( Decal decal )
	{
		var id = decal.GameObject.Id.ToByteArray();
		return ((decal.SortLayer & 0xFF) << 24) | (uint)(id[0] | (id[1] << 8) | (id[2] << 16));
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

	static GameObject CreateChild( GameObject parent, string name )
	{
		var go = new GameObject( false, name );
		go.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked | GameObjectFlags.Hidden;
		go.NetworkMode = NetworkMode.Never;
		go.SetParent( parent, false );
		return go;
	}

	/// <summary>
	/// <see cref="Decal.SortLayer"/> only keeps 8 bits, past that everything sits on top rather than wrapping under older decals
	/// </summary>
	Decal CreateDecal( string name, Transform transform, uint layer )
	{
		var decal = CreateChild( GameObject, name ).AddComponent<Decal>();
		decal.GameObject.LocalTransform = transform;
		decal.SortLayer = Math.Min( layer, 255 );
		return decal;
	}

	Renderer CreateSingle( DecalStamp stamp )
	{
		var decal = CreateDecal( "decal", stamp.LocalTransform, stamp.Layer );
		decal.Decals = [stamp.Definition];
		decal.ColorTint = stamp.Tint;
		decal.Scale = stamp.Scale;
		decal.Rotation = stamp.Rotation;
		decal.AttenuationAngle = stamp.Attenuation;
		decal.GameObject.Enabled = true;

		return new Renderer( decal.GameObject, [stamp.Id] );
	}

	/// <summary>
	/// One decal covering every stamp in the group, hidden while a command list on <paramref name="camera"/> bakes its textures
	/// before the opaque pass
	/// </summary>
	Renderer CreateMerged( List<DecalStamp> stamps, CameraComponent camera )
	{
		// faces the way the first stamp does, grown on its plane to fit all of them
		var source = stamps[0].Definition;
		var frame = stamps[0].LocalTransform;
		var min = new Vector2( float.MaxValue );
		var max = new Vector2( float.MinValue );
		var extents = new List<(Vector2 Min, Vector2 Max)>();
		var density = 0f;

		foreach ( var stamp in stamps )
		{
			var box = stamp.Box;
			var lo = new Vector2( float.MaxValue );
			var hi = new Vector2( float.MinValue );
			foreach ( var corner in Corners )
			{
				var p = frame.PointToLocal( box.PointToWorld( corner ) );
				lo = Vector2.Min( lo, new Vector2( p.y, p.z ) );
				hi = Vector2.Max( hi, new Vector2( p.y, p.z ) );
			}

			extents.Add( (lo, hi) );
			min = Vector2.Min( min, lo );
			max = Vector2.Max( max, hi );

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

		// colour in float for the dark end, the rest is data that 8 bits (or one channel) holds fine.
		// A channel only exists if the stamps have it, they all match
		(string Name, Texture Canvas, Color Clear, Func<DecalDefinition, Texture> Source)[] channels =
		[
			("Color", Canvas( source.ColorTexture, ImageFormat.RGBA16161616F ), Color.Transparent, x => x.ColorTexture),
			("Normal", Canvas( source.NormalTexture, ImageFormat.RGBA8888_LINEAR ), new Color( 0.5f, 0.5f, 1f ), x => x.NormalTexture),
			("Rmo", Canvas( source.RoughMetalOcclusionTexture, ImageFormat.RGBA8888_LINEAR ), Color.White, x => x.RoughMetalOcclusionTexture),
			("Height", Canvas( source.HeightTexture, ImageFormat.R16F ), Color.White, x => x.HeightTexture),
		];
		Texture Canvas( Texture used, ImageFormat format ) => used.IsValid() ? Texture.Create( width, height, format ).WithUAVBinding().WithMips().Finish() : null;

		var used = channels.Where( x => x.Canvas is not null ).ToArray();
		var parallax = source.HeightTexture.IsValid() ? stamps.Max( x => x.Definition.ParallaxStrength ) : 0;

		// clears first, so running more than once (several frames, reflection views) still comes out the same
		var commands = new CommandList( "DecalMerge" );
		var a = commands.Attributes;
		foreach ( var channel in channels )
			a.Set( $"Has{channel.Name}", channel.Canvas is null ? 0 : 1 );

		foreach ( var channel in used )
		{
			commands.Clear( channel.Canvas, channel.Clear );
			a.Set( $"{channel.Name}Out", channel.Canvas );
		}

		var targetBasis = TangentBasis( target.Rotation );

		for ( var i = 0; i < stamps.Count; i++ )
		{
			var def = stamps[i].Definition;
			var box = stamps[i].Box;

			// only the texels under this stamp, the merged decal's uv runs opposite to its local y and z
			var (lo, hi) = extents[i];
			var x0 = Math.Max( 0, (int)((max.x - hi.x) / size.x * width) );
			var y0 = Math.Max( 0, (int)((max.y - hi.y) / size.y * height) );
			var x1 = Math.Min( width, (int)MathF.Ceiling( (max.x - lo.x) / size.x * width ) );
			var y1 = Math.Min( height, (int)MathF.Ceiling( (max.y - lo.y) / size.y * height ) );
			if ( x1 <= x0 || y1 <= y0 ) continue;

			// the decal shader samples at uv = 0.5 - local.yz, in both decals
			Vector2 StampUv( float x, float y )
			{
				var local = box.PointToLocal( target.PointToWorld( new Vector3( 0, 0.5f - x / width, 0.5f - y / height ) ) );
				return new Vector2( 0.5f - local.y, 0.5f - local.z );
			}

			var origin = StampUv( 0, 0 );
			var basis = TangentBasis( box.Rotation );
			Vector3 Row( Vector3 axis ) => new( axis.Dot( basis.x ), axis.Dot( basis.y ), axis.Dot( basis.z ) );

			foreach ( var channel in used )
				a.Set( channel.Name, channel.Source( def ) );

			a.Set( "Offset", new Vector2( x0, y0 ) );
			a.Set( "Size", new Vector2( x1 - x0, y1 - y0 ) );
			a.Set( "Origin", origin );
			a.Set( "StepX", StampUv( 1, 0 ) - origin );
			a.Set( "StepY", StampUv( 0, 1 ) - origin );
			a.Set( "Tint", def.Tint * stamps[i].Tint );
			a.Set( "BasisX", Row( targetBasis.x ) );
			a.Set( "BasisY", Row( targetBasis.y ) );
			a.Set( "BasisZ", Row( targetBasis.z ) );
			a.Set( "CoverageAmount", def.CoverageAmount );
			a.Set( "DepthScale", parallax > 0 ? def.ParallaxStrength / parallax : 0 );

			commands.DispatchCompute( BakeShader, x1 - x0, y1 - y0, 1 );

			// the next stamp blends over what this one wrote
			foreach ( var channel in used )
				commands.UavBarrier( channel.Canvas );
		}

		foreach ( var channel in used )
			commands.GenerateMipMaps( channel.Canvas, Graphics.DownsampleMethod.Box );

		camera.AddCommandList( commands, Stage.AfterDepthPrepass );

		// ponytail: ColorMix below 1 mixes by ColorMix * merged alpha, a little weaker than separate decals where they overlap
		var decal = CreateDecal( "merged decal", transform, stamps[^1].Layer );
		decal.Decals = [new DecalDefinition
		{
			ColorTexture = channels[0].Canvas,
			NormalTexture = channels[1].Canvas,
			RoughMetalOcclusionTexture = channels[2].Canvas,
			HeightTexture = channels[3].Canvas,
			ParallaxStrength = parallax,
			ColorMix = source.ColorMix,
			FilterMode = source.FilterMode,
			Width = size.x,
			Height = size.y,
		}];
		decal.Rotation = 0;
		decal.AttenuationAngle = stamps[0].Attenuation;

		return new Renderer( decal.GameObject, [.. stamps.Select( x => x.Id )], [.. used.Select( x => x.Canvas )], commands, camera, PerformanceStats.GpuFrameNumber );
	}

	/// <summary>
	/// Where a decal's tangent space normal points, per <c>Decals.hlsl</c>: TransformNormal with -forward as the normal,
	/// -left and -up as tangents, and the tangent y flipped
	/// </summary>
	static (Vector3 x, Vector3 y, Vector3 z) TangentBasis( Rotation rotation ) => (-rotation.Left, rotation.Up, -rotation.Forward);
}
