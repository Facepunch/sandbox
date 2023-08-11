using Sandbox;
using Sandbox.Component;
using System.Linq;
using System;

public partial class PhysGun
{
	CapsuleLightEntity? BeamLight;
	Particles Beam;
	Particles EndNoHit;

	Vector3 lastBeamPos;
	ModelEntity lastGrabbedEntity;

	[GameEvent.Client.Frame]
	public void OnFrame()
	{
		UpdateEffects();
	}

	protected virtual void KillEffects()
	{
		BeamLight?.Delete();
		Beam?.Destroy( true );
		Beam = null;
		BeamActive = false;
		BeamLight = null;

		EndNoHit?.Destroy( false );
		EndNoHit = null;

		if ( lastGrabbedEntity.IsValid() )
		{
			foreach ( var child in lastGrabbedEntity.Children.OfType<ModelEntity>() )
			{
				if ( child is Player )
					continue;

				if ( child.Components.TryGet<Glow>( out var childglow ) )
				{
					childglow.Enabled = false;
				}
			}

			if ( lastGrabbedEntity.Components.TryGet<Glow>( out var glow ) )
			{
				glow.Enabled = false;
			}

			lastGrabbedEntity = null;
		}
	}

	protected virtual void UpdateEffects()
	{
		var owner = Owner as Player;

		if ( owner == null || !BeamActive || owner.ActiveChild != this )
		{
			KillEffects();
			return;
		}

		var startPos = owner.EyePosition;
		var dir = owner.EyeRotation.Forward;

		var tr = Trace.Ray( startPos, startPos + dir * MaxTargetDistance )
			.UseHitboxes()
			.Ignore( owner, false )
			.WithAnyTags( "solid", PhysgunBlockTag )
			.OnTraceEvent( Owner ) // SandboxPlus addition for Stargate support
			.Run();

		Beam ??= Particles.Create( "particles/physgun_beam.vpcf", tr.EndPosition );

		if ( !BeamLight.IsValid() )
		{
			BeamLight = new CapsuleLightEntity
			{
				Color = Color.FromBytes( 4, 20, 70 )
			};
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
					Beam.SetPosition( 1, physBody.Transform.PointToWorld( GrabbedPos ) );
				}
			}
			else
			{
				Beam.SetEntity( 1, GrabbedEntity, GrabbedPos, true );
			}

			lastBeamPos = GrabbedEntity.Position + GrabbedEntity.Rotation * GrabbedPos;

			EndNoHit?.Destroy( false );
			EndNoHit = null;

			if ( GrabbedEntity is ModelEntity modelEnt )
			{
				lastGrabbedEntity = modelEnt;

				var glow = modelEnt.Components.GetOrCreate<Glow>();
				glow.Enabled = true;
				glow.Width = 0.25f;
				glow.Color = new Color( 4f, 50.0f, 70.0f, 1.0f );
				glow.ObscuredColor = new Color( 4f, 50.0f, 70.0f, 0.0005f );

				foreach ( var child in lastGrabbedEntity.Children.OfType<ModelEntity>() )
				{
					if ( child is Player )
						continue;

					glow = child.Components.GetOrCreate<Glow>();
					glow.Enabled = true;
					glow.Color = new Color( 0.1f, 1.0f, 1.0f, 1.0f );
				}
			}
		}
		else
		{
			lastBeamPos = tr.EndPosition;// Vector3.Lerp( lastBeamPos, tr.EndPosition, Time.Delta * 10 );
			Beam.SetPosition( 1, lastBeamPos );

			if ( EndNoHit == null )
				EndNoHit = Particles.Create( "particles/physgun_end_nohit.vpcf", lastBeamPos );

			EndNoHit.SetPosition( 0, lastBeamPos );
		}

		if ( BeamLight.IsValid() )
		{
			var muzzle = IsFirstPersonMode && ViewModelEntity.IsValid ? ViewModelEntity.GetAttachment( "muzzle" ) ?? default : GetAttachment( "muzzle" ) ?? default;
			var pos = muzzle.Position;

			BeamLight.CapsuleLength = ( pos - lastBeamPos ).Length * 0.5f;
			BeamLight.LightSize = 0.5f + ( MathF.Sin( Time.Now * 10 ) * 0.5f);
			BeamLight.Position = ( pos + lastBeamPos ) * 0.5f;
			BeamLight.Rotation = Rotation.LookAt( ( pos - lastBeamPos ).Normal );

			BeamLight.Color = Color.Lerp( BeamLight.Color, new Color( 0.1f, 1.0f, 1.0f, 1.0f ) * 0.039f, Time.Delta * 10 );
		}
		
	}
}
