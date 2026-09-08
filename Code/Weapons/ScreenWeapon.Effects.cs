public partial class ScreenWeapon
{
	[Header( "Effects" )]
	[Property] public GameObject SuccessImpactEffect { get; set; }
	[Property] public GameObject SuccessBeamEffect { get; set; }

	/// <summary>Play the shared tool beam (including its sound), impact and viewmodel animation.</summary>
	public void PlayToolEffects( Transform target )
	{
		SpinCoil();

		var muzzle = GetMuzzleTransform();

		if ( SuccessImpactEffect is GameObject impactPrefab )
		{
			var wt = target;
			wt.Rotation = wt.Rotation * new Angles( 90, 0, 0 );

			var impact = impactPrefab.Clone( wt, null, false );
			impact.Enabled = true;
		}

		if ( SuccessBeamEffect is GameObject beamEffect )
		{
			var wt = target;

			var go = beamEffect.Clone( new Transform( muzzle.Position ), null, false );

			foreach ( var beam in go.GetComponentsInChildren<BeamEffect>( true ) )
			{
				beam.TargetPosition = wt.Position;
			}

			go.Enabled = true;
		}

		ViewModel?.GetComponentInChildren<SkinnedModelRenderer>()?.Set( "b_attack", true );
	}
}
