public sealed class TemporaryDecal : Component
{
	float createdTime;

	protected override void OnEnabled()
	{
		createdTime = RealTime.Now;

		base.OnEnabled();

		if ( GameSettings.MaxDecals > 1 )
		{
			var count = Scene.GetAll<TemporaryDecal>().Count();

			while ( count > GameSettings.MaxDecals )
			{
				count--;

				Scene.GetAll<TemporaryDecal>()
							.OrderBy( x => x.createdTime )
							.First()
							.GameObject
							.DestroyImmediate();

			}
		}
	}

	[ConCmd( "r_cleardecals" )]
	public static void ClearDecals()
	{
		foreach ( var decal in Game.ActiveScene.GetAll<TemporaryDecal>() )
		{
			decal.DestroyGameObject();
		}
	}
}
