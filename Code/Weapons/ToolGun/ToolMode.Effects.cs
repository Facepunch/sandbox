public abstract partial class ToolMode
{
	[Rpc.Broadcast]
	public virtual void ShootEffects( SelectionPoint target )
	{
		var player = Toolgun.Owner;
		if ( player is null ) return;
		if ( !target.IsValid() )
		{
			Log.Warning( "ShootEffects: Unknown object" );
			return;
		}

		var muzzle = Toolgun.MuzzleTransform;


		if ( Toolgun.SuccessImpactEffect is GameObject impactPrefab )
		{
			var wt = target.WorldTransform();
			wt.Rotation = wt.Rotation * new Angles( 90, 0, 0 );

			var impact = impactPrefab.Clone( wt, null, false );
			impact.Enabled = true;
		}

		if ( Toolgun.SuccessBeamEffect is GameObject beamEffect )
		{
			var wt = target.WorldTransform();

			var go = beamEffect.Clone( new Transform( muzzle.WorldTransform.Position ), null, false );

			foreach ( var beam in go.GetComponentsInChildren<BeamEffect>( true ) )
			{
				beam.TargetPosition = wt.Position;
			}

			go.Enabled = true;
		}

		Toolgun.ViewModel?.GetComponentInChildren<SkinnedModelRenderer>().Set( "b_attack", true );
	}

	public virtual void ShootFailEffects( SelectionPoint target )
	{

	}

}

