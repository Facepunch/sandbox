using Sandbox;
using System.Linq;

public partial class PhysGun
{
	Particles Beam;
	Particles EndNoHit;

	Vector3 lastBeamPos;
	ModelEntity lastGrabbedEntity;

	protected virtual void KillEffects()
	{
		Beam?.Destroy( true );
		Beam = null;
		BeamActive = false;

		EndNoHit?.Destroy( false );
		EndNoHit = null;

		if ( lastGrabbedEntity.IsValid() )
		{
			foreach ( var child in lastGrabbedEntity.Children.OfType<ModelEntity>() )
			{
				child.GlowActive = false;
				child.GlowState = GlowStates.GlowStateOff;
			}

			lastGrabbedEntity.GlowActive = false;
			lastGrabbedEntity.GlowState = GlowStates.GlowStateOff;
			lastGrabbedEntity = null;
		}
	}

	[Event("frame")]
	protected void UpdateEffects()
	{
		var owner = Owner;

		if ( owner == null || !BeamActive || !IsActiveChild() )
		{
			KillEffects();
			return;
		}

		var startPos = owner.EyePos;
		var dir = owner.EyeRot.Forward;

		var tr = Trace.Ray( startPos, startPos + dir * MaxTargetDistance )
			.UseHitboxes()
			.Ignore( owner )
			.Run();

		if ( Beam == null )
		{
			Beam = Particles.Create( "particles/physgun_beam.vpcf", tr.EndPos );
		}

		Beam.SetEntityAttachment( 0, EffectEntity, "muzzle", true );

		if ( GrabbedEntity.IsValid() && !GrabbedEntity.IsWorld )
		{
			var physGroup = GrabbedEntity.PhysicsGroup;

			if ( physGroup != null && GrabbedBone >= 0 )
			{
				var physBody = physGroup.GetBody( GrabbedBone );
				if ( physBody != null )
				{
					Beam.SetPos( 1, physBody.Transform.PointToWorld( GrabbedPos ) );
				}
			}
			else
			{
				Beam.SetEntity( 1, GrabbedEntity, GrabbedPos, true );
			}

			lastBeamPos = GrabbedEntity.WorldPos + GrabbedEntity.WorldRot * GrabbedPos;

			EndNoHit?.Destroy( false );
			EndNoHit = null;

			if ( GrabbedEntity is ModelEntity modelEnt )
			{
				lastGrabbedEntity = modelEnt;
				modelEnt.GlowState = GlowStates.GlowStateOn;
				modelEnt.GlowDistanceStart = 0;
				modelEnt.GlowDistanceEnd = 1000;
				modelEnt.GlowColor = new Color( 0.1f, 1.0f, 1.0f, 1.0f );
				modelEnt.GlowActive = true;

				foreach ( var child in lastGrabbedEntity.Children.OfType<ModelEntity>() )
				{
					child.GlowState = GlowStates.GlowStateOn;
					child.GlowDistanceStart = 0;
					child.GlowDistanceEnd = 1000;
					child.GlowColor = new Color( 0.1f, 1.0f, 1.0f, 1.0f );
					child.GlowActive = true;
				}
			}
		}
		else
		{
			lastBeamPos = tr.EndPos;// Vector3.Lerp( lastBeamPos, tr.EndPos, Time.Delta * 10 );
			Beam.SetPos( 1, lastBeamPos );

			if ( EndNoHit == null )
				EndNoHit = Particles.Create( "particles/physgun_end_nohit.vpcf", lastBeamPos );

			EndNoHit.SetPos( 0, lastBeamPos );
		}
	}
}
