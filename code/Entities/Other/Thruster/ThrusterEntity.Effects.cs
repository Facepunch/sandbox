using Sandbox;

public partial class ThrusterEntity
{
	private Particles effects;

	[Event.Frame]
	public void OnFrame()
	{
		UpdateEffects();
	}

	protected void CreateEffects()
	{
		if ( effects is not null )
			return;

		effects = Particles.Create( "particles/physgun_end_nohit.vpcf" );
	}

	protected virtual void KillEffects()
	{
		if ( effects is null )
			return;

		effects.Destroy( false );
		effects = null;
	}

	protected virtual void UpdateEffects()
	{
		if ( Enabled )
		{
			CreateEffects();
		}
		else
		{
			KillEffects();
		}

		if ( effects is null )
			return;

		effects.SetPosition( 0, Position + Rotation.Up * 20 );
	}
}
