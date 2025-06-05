namespace Sandbox.CameraNoise;

class Punch : BaseCameraNoise
{
	float deathTime;
	float lifeTime = 0.0f;

	public Punch( Vector3 target, float time, float frequency, float damp )
	{
		damping.Current = target * GamePreferences.Screenshake;
		damping.Target = 0;
		damping.SmoothTime = time;
		damping.Frequency = frequency;
		damping.Damping = damp;

		deathTime = damping.SmoothTime;
	}

	public override bool IsDone => deathTime <= 0;

	Vector3.SpringDamped damping;

	public override void Update()
	{
		deathTime -= Time.Delta;
		lifeTime += Time.Delta;

		damping.Update( Time.Delta );
	}

	public override void ModifyCamera( CameraComponent cc )
	{
		var amount = lifeTime.Remap( 0, 0.3f, 0, 1 );

		cc.WorldRotation *= new Angles( damping.Current * amount );
	}
}
