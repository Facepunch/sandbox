using Sandbox;

public partial class Tool
{
	[ClientRpc]
	public void CreateHitEffects( Vector3 hitPos )
	{
		var particle = Particles.Create( "particles/tool_hit.vpcf", hitPos );
		particle.SetPosition( 0, hitPos );
		particle.Destroy( false );

		PlaySound( "balloon_pop_cute" );
	}
}
