using Sandbox.Utility;

namespace Sandbox.CameraNoise;

class Shake : BaseCameraNoise
{
	float lifeTime;
	float deathTime;
	float amount = 0.0f;

	public Shake( float amount, float time )
	{
		this.amount = amount * GamePreferences.Screenshake;
		deathTime = time;
		lifeTime = time;
	}

	public override bool IsDone => deathTime <= 0;

	Vector3.SpringDamped damping;

	public override void Update()
	{
		deathTime -= Time.Delta;
		damping.Update( Time.Delta );
	}

	public override void ModifyCamera( CameraComponent cc )
	{
		var x = Noise.Perlin( Time.Now * 1000.0f, 2345 );
		var y = Noise.Perlin( Time.Now * 1000.0f, 21 );
		var z = Noise.Perlin( Time.Now * 1000.0f, 865 );

		var delta = MathX.Remap( deathTime, 0, lifeTime, 0, 1 );

		cc.WorldRotation *= new Angles( x, y, z ) * delta * amount;
	}
}
