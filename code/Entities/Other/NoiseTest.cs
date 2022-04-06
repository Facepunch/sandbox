using Sandbox;

[Library( "noise_test", Title = "Noise Test", Spawnable = true )]
public partial class NoiseTest : Prop
{
	public override void Spawn()
	{
		base.Spawn();

		SetModel( "models/citizen_props/balloonregular01.vmdl" );
		SetupPhysicsFromModel( PhysicsMotionType.Dynamic, false );
	}

	[Event.Frame]
	public void OnFrame()
	{
		var pos = Position;
		var right = Rotation.Right * 4;
		var forward = Rotation.Forward * 4;
		var up = Rotation.Up * 50;
		var offset = Time.Now * 2.0f;
		var offsetz = Time.Now * 0.1f;

		var mode = (int)((Time.Now * 0.3f) % 5);

		switch ( mode )
		{
			case 0:
				{
					DebugOverlay.Text( pos, "Perlin" );
					break;
				}

			case 1:
				{
					DebugOverlay.Text( pos, "SparseConvolution" );
					break;
				}

			case 2:
				{
					DebugOverlay.Text( pos, "SparseConvolutionNormalized" );
					break;
				}

			case 3:
				{
					DebugOverlay.Text( pos, "Turbulence" );
					break;
				}

			case 4:
				{
					DebugOverlay.Text( pos, "Fractal" );
					break;
				}
		}


		var size = 100;

		pos -= right * size * 0.5f;
		pos -= forward * size * 0.5f;

		for ( float x = 0; x < size; x++ )
			for ( float y = 0; y < size; y++ )
			{
				float val = 0;

				switch ( mode )
				{
					case 0:
						{
							val = Noise.Perlin( x * 0.1f + offset, y * 0.1f, offsetz ) * 0.5f;
							break;
						}
					case 1:
						{
							val = Noise.SparseConvolution( x * 0.1f + offset, y * 0.1f, offsetz ) * 0.5f;
							break;
						}
					case 2:
						{
							val = Noise.SparseConvolutionNormalized( x * 0.1f + offset, y * 0.1f, offsetz ) * 0.5f;
							break;
						}
					case 3:
						{
							val = Noise.Turbulence( 2, x * 0.1f + offset, y * 0.1f, offsetz ) * 0.5f;
							break;
						}
					case 4:
						{
							val = Noise.Fractal( 2, x * 0.1f + offset, y * 0.1f, offsetz ) * 0.5f;
							break;
						}
				}

				var start = pos + x * right + y * forward;
				DebugOverlay.Line( start, start + up * val, Color.Lerp( Color.Red, Color.Green, (val + 1.0f) / 2.0f ) );
			}
	}
}
