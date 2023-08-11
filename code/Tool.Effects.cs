using System;
using Sandbox;
using Sandbox.UI;

public partial class Tool
{
	static readonly string[] ShootSounds = new string[]
	{
		// It feels super wrong not to use a path here, but, this is apparently how .sound files work at the moment.
		"airboat_gun_lastshot1",
		"airboat_gun_lastshot2",
	};

	[ClientRpc]
	public void CreateHitEffects( Vector3 hitPos, Vector3 normal = new Vector3(), bool continuous = false )
	{
		Game.AssertClient();

		if ( continuous )
		{
			var particle = Particles.Create( "particles/tool_hit.vpcf", hitPos );
			particle.SetPosition( 0, hitPos );
			particle.Destroy( false );
		}
		else
		{
			if ( normal.Length > 0 )
			{
				var random = new Random();
				var offset = normal * 0.66f; // A number of models have surfaces that differ from the physics hull, so let's add a small offset so the particle is always visible
				var particle = Particles.Create( "particles/tool_select_indicator.vpcf", hitPos + offset );
				particle.SetPosition( 0, hitPos + offset );
				particle.SetForward( 1, normal );
				particle.SetPosition( 2, new Vector3( // Actually a color. Blame Facepunch for calling it "SetPos".
													  // These values are taken from Garry's Mod, and yet, they seem wrong...
					random.Next( 10, 150 ) / 255.0f,
					random.Next( 170, 220 ) / 255.0f,
					random.Next( 240, 255 ) / 255.0f
				) );
			}

			var particle2 = Particles.Create( "particles/tool_hit.vpcf", EffectEntity, "muzzle" ); ;
			particle2.Destroy( false );

			var beam = Particles.Create( "particles/tool_tracer.vpcf", hitPos );
			beam.SetEntityAttachment( 0, EffectEntity, "muzzle", true );
			beam.SetPosition( 1, hitPos );

			ViewModelEntity?.SetAnimParameter( "fire", true );
			// CrosshairPanel?.OnEvent( "onattack" );

			int soundIndex = new Random().Next( 0, ShootSounds.Length );
			PlaySound( ShootSounds[soundIndex] );
		}
	}
}
