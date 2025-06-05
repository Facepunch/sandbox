using Sandbox.Utility;

namespace Sandbox.CameraNoise;

/// <summary>
/// Creates a bunch of other common effects
/// </summary>
class Recoil : BaseCameraNoise
{
	public Recoil( float amount, float speed = 1 )
	{
		new RollShake() { Size = 0.5f * amount * GamePreferences.Screenshake, Waves = 3 * speed };
	}

	public override void ModifyCamera( CameraComponent cc )
	{
	}
}

/// <summary>
/// Shake the screen in a roll motion
/// </summary>
class RollShake : BaseCameraNoise
{
	public float Size { get; set; } = 3.0f;
	public float Waves { get; set; } = 3.0f;

	public RollShake()
	{
		LifeTime = 0.3f;
	}

	public override void ModifyCamera( CameraComponent cc )
	{
		var delta = Delta;
		var s = MathF.Sin( delta * MathF.PI * Waves * 2 );
		cc.WorldRotation *= new Angles( 0, 0, s * Size ) * Easing.EaseOut( DeltaInverse );
	}
}
