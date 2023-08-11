using Sandbox;

public partial class ThrusterEntity
{
	private Particles effects;

	[Event.Client.Frame]
	public void OnFrame()
	{
		UpdateEffects();
	}

	protected void CreateEffects()
	{
		if ( effects != null )
			return;

		effects = Particles.Create( "particles/physgun_end_nohit.vpcf" );
	}

	protected virtual void KillEffects()
	{
		if ( effects == null )
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

		if ( effects == null )
			return;


		effects.SetPosition( 0, Position + Rotation.Up * CollisionBounds.Size.z );
	}
}
