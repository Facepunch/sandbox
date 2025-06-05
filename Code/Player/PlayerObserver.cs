/// <summary>
/// Dead players become these. They try to observe their last corpse. 
/// </summary>
public sealed class PlayerObserver : Component
{
	Angles EyeAngles;
	TimeSince timeSinceStarted;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		EyeAngles = Scene.Camera.WorldRotation;
		timeSinceStarted = 0;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		var corpse = Scene.GetAllComponents<DeathCameraTarget>()
					.Where( x => x.Connection == Network.Owner )
					.OrderByDescending( x => x.Created )
					.FirstOrDefault();

		if ( corpse.IsValid() )
		{
			RotateAround( corpse );
		}

		// Don't allow immediate respawn
		if ( timeSinceStarted < 1 )
			return;

		// If pressed a button, or has been too long
		if ( Input.Pressed( "attack1" ) || Input.Pressed( "jump" ) || timeSinceStarted > 4f )
		{
			Respawn();
			GameObject.Destroy();
		}
	}

	[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Reliable )]
	public void Respawn()
	{
		GameManager.Current.SpawnPlayer( Network.Owner );
		GameObject.Destroy();
	}

	private void RotateAround( Component target )
	{
		// Find the corpse eyes
		if ( !target.Components.Get<SkinnedModelRenderer>().TryGetBoneTransform( "head", out var tx ) )
		{
			tx.Position = target.GameObject.GetBounds().Center + Vector3.Up * 25f;
		}

		var e = EyeAngles;
		e += Input.AnalogLook;
		e.pitch = e.pitch.Clamp( -90, 90 );
		e.roll = 0.0f;
		EyeAngles = e;

		var center = tx.Position;
		var targetPos = center - EyeAngles.Forward * 150f;

		var tr = Scene.Trace.FromTo( center, targetPos ).Radius( 1.0f ).WithoutTags( "ragdoll", "effect" ).Run();

		Scene.Camera.WorldPosition = Vector3.Lerp( Scene.Camera.WorldPosition, tr.EndPosition, timeSinceStarted, true );
		Scene.Camera.WorldRotation = EyeAngles;
	}
}
