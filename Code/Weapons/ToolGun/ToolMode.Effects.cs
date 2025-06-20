public abstract partial class ToolMode
{
	[Rpc.Broadcast]
	public virtual void ShootEffects( SelectionPoint target )
	{
		var prefab = Toolgun.SuccessImpactEffect;
		if ( prefab is null ) return;

		var wt = target.WorldTransform();
		wt.Rotation = wt.Rotation * new Angles( 90, 0, 0 );

		var impact = prefab.Clone( wt, null, false );
		impact.Enabled = true;
	}

	public virtual void ShootFailEffects( SelectionPoint target )
	{

	}

}

