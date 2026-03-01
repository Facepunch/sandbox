using Sandbox.Rendering;
using Sandbox.Utility;

public partial class Physgun : ScreenWeapon
{
	[Property] public LineRenderer BeamRenderer { get; set; }
	[Property] public GameObject EndPointEffectPrefab { get; set; }
	[Property] public GameObject FreezeEffectPrefab { get; set; }
	[Property] public GameObject UnFreezeEffectPrefab { get; set; }
	[Property] public GameObject GrabEffectPrefab { get; set; }

	protected override string ScreenMaterialName => "v_physgun_display";
	protected override string ScreenMaterialPath => "weapons/physgun/physgun-screen.vmat";
	protected override float ScreenRefreshInterval => 0.05f;
	protected override Vector2Int ScreenTextureSize => new Vector2Int( 80, 80 );

	Vector3.SpringDamped middleSpring = new Vector3.SpringDamped( 0, 0 );

	GameObject _endPointEffect;
	GameObject _grabEffect;

	public bool BeamActive => BeamRenderer?.Active == true || _state.Pulling || _stateHovered.Pulling;
	public bool PullActive => _state.Pulling || _stateHovered.Pulling;

	void UpdateBeam( Transform source, Vector3 end, Vector3 endNormal, bool grabbed )
	{
		if ( !BeamRenderer.IsValid() ) return;

		var endTx = new Transform( end, Rotation.LookAt( endNormal ) );

		if ( grabbed )
		{
			if ( _endPointEffect != null )
			{
				ITemporaryEffect.DisableLoopingEffects( _endPointEffect );
				_endPointEffect = null;
			}


			if ( !_grabEffect.IsValid() )
			{
				_grabEffect = GrabEffectPrefab.Clone( endTx );
			}

			if ( _grabEffect.IsValid() )
			{
				_grabEffect.WorldTransform = endTx;
			}

		}
		else
		{
			if ( _grabEffect != null )
			{
				_grabEffect.Destroy();
				_grabEffect = null;
			}

			if ( !_endPointEffect.IsValid() )
			{
				_endPointEffect = EndPointEffectPrefab.Clone( endTx );
			}

			if ( _endPointEffect.IsValid() )
			{
				_endPointEffect.WorldTransform = endTx;
			}
		}

		// obj
		if ( _state.GameObject.IsValid() )
		{
			//	BeamHighlight.Enabled = true;
			//	BeamHighlight.OverrideTargets = true;
			//	BeamHighlight.Targets.Clear();
			//	BeamHighlight.Targets.AddRange( _state.GameObject.GetComponents<Renderer>() );
			//	BeamHighlight.Width = 0.1f + Noise.Fbm( 3, Time.Now * 100.0f ) * 0.1f;
			//	BeamHighlight.Color = Color.Lerp( Color.Cyan, Color.White, Noise.Fbm( 3, Time.Now * 40.0f ) * 0.5f ) * 200.0f;
		}

		bool justEnabled = !BeamRenderer.GameObject.Enabled;

		if ( BeamRenderer.VectorPoints.Count != 4 )
			BeamRenderer.VectorPoints = new List<Vector3>( [0, 0, 0, 0] );

		var distance = source.Position.Distance( end );

		BeamRenderer.VectorPoints[0] = source.Position;


		var targetMiddle = source.Position + source.Forward * distance * 0.33f;
		targetMiddle = targetMiddle + Noise.FbmVector( 2, Time.Now * 400.0f, Time.Now * 100.0f ) * 1.0f;

		BeamRenderer.VectorPoints[1] = middleSpring.Current;
		middleSpring.Target = targetMiddle;
		middleSpring.Update( Time.Delta );

		BeamRenderer.VectorPoints[2] = Vector3.Lerp( (end + endNormal * 10), BeamRenderer.VectorPoints[1], 0.3f + MathF.Sin( Time.Now * 10.0f ) * 0.2f );
		BeamRenderer.VectorPoints[3] = end;

		if ( justEnabled )
		{
			BeamRenderer.GameObject.Enabled = true;
			BeamRenderer.VectorPoints[1] = targetMiddle;
			middleSpring = new Vector3.SpringDamped( targetMiddle, targetMiddle, 4, 0.2f );
		}


	}

	void CloseBeam()
	{
		if ( _stateHovered.GameObject.IsValid() )
		{
			//	BeamHighlight.Enabled = true;
			//	BeamHighlight.OverrideTargets = true;
			//	BeamHighlight.Targets.Clear();
			//	BeamHighlight.Targets.AddRange( _stateHovered.GameObject.GetComponents<Renderer>() );
			//	BeamHighlight.Width = 0.2f;
			//	BeamHighlight.Color = new Color( 0.5f, 1, 1, 0.3f );
		}
		else
		{
			BeamHighlight.Enabled = false;
		}

		if ( !BeamRenderer.IsValid() ) return;

		BeamRenderer.GameObject.Enabled = false;

		if ( _endPointEffect.IsValid() )
		{
			ITemporaryEffect.DisableLoopingEffects( _endPointEffect );
			_endPointEffect = null;
		}

		if ( _grabEffect.IsValid() )
		{
			_grabEffect.Destroy();
			_grabEffect = null;
		}
	}

	private const int GraphSamples = 128;
	private float[] _graph1 = new float[GraphSamples];
	private float[] _graph2 = new float[GraphSamples];
	private float[] _graph3 = new float[GraphSamples];
	private int _graphCursor;
	private float _graphTimer;
	private const float GraphInterval = 0.02f;

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
		var padX = w * 0.05f;
		var padY = h * 0.15f;

		var barWidthFraction = 0.55f;
		var barHeightFraction = 0.1f;

		var barW = w * barWidthFraction;
		var barH = h * barHeightFraction;
		var barX = rect.Left + padX;
		var barY = rect.Top + padY;

		var borderColor = new Color( 0.5f, 0.5f, 0.5f );

		// Fill bar
		var fillFraction = MathF.Max( _plotValue1, _plotValue2 );
		var normalized = ((fillFraction - 0.1f) / (0.8f - 0.1f)).Clamp( 0f, 1f );
		var fillWidth = barW * normalized;
		if ( fillWidth > 0f )
		{
			paint.DrawRect( new Rect( barX, barY, fillWidth, barH ), new Color( 1, 1, 1, 0.8f ) );
		}

		// Bar outline
		paint.DrawLine( new Vector2( barX, barY ), new Vector2( barX + barW, barY ), 1f, borderColor );
		paint.DrawLine( new Vector2( barX, barY + barH ), new Vector2( barX + barW, barY + barH ), 1f, borderColor );
		paint.DrawLine( new Vector2( barX, barY ), new Vector2( barX, barY + barH ), 1f, borderColor );
		paint.DrawLine( new Vector2( barX + barW, barY ), new Vector2( barX + barW, barY + barH ), 1f, borderColor );

		// Percentage label
		var percent = (int)(normalized * 100f);
		var percentLabel = new TextRendering.Scope( $"{percent}", Color.White, h * 0.135f );
		percentLabel.FontName = "Consolas";
		percentLabel.TextColor = Color.White;
		percentLabel.FontWeight = 100;
		percentLabel.FilterMode = FilterMode.Point;
		paint.DrawText( percentLabel, new Rect( barX + barW + padX, barY, w - barW - padX * 3f, barH ), TextFlag.LeftCenter );

		// Channel / voltage row
		var rowY = barY + barH + padY;

		var ch2 = new TextRendering.Scope( "Ch2", Color.White, h * 0.14f );
		ch2.FontName = "Consolas";
		ch2.TextColor = new Color( 0f, 1f, 0f );
		ch2.FontWeight = 400;
		ch2.FilterMode = FilterMode.Point;
		paint.DrawText( ch2, new Rect( barX, rowY, w * 0.45f, 0 ), TextFlag.LeftCenter );

		var voltage = new TextRendering.Scope( "731v", Color.White, h * 0.14f );
		voltage.FontName = "Consolas";
		voltage.TextColor = new Color( 0f, 1f, 0f );
		voltage.FontWeight = 400;
		voltage.FilterMode = FilterMode.Point;
		paint.DrawText( voltage, new Rect( barX + w * 0.45f, rowY, w * 0.45f, 0 ), TextFlag.LeftCenter );
	}

	private TimeSince _lastGraphUpdate;

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

		if ( _lastGraphUpdate < ScreenRefreshInterval )
			return;

		_lastGraphUpdate = 0;

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
}
