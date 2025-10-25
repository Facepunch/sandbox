public sealed class PlayerDamageIndicators : Component, IPlayerEvent
{
	[RequireComponent] Player Player { get; set; }

	float RadialDistanceFromCenter => 128f;
	float RadialIndicatorLifetime => 2f;

	TimeSince timeSinceHurt = 10f;
	List<(Vector3 WorldPos, TimeSince Lifetime)> radialIndicators = new();

	[Property] public Texture RadialDamageIcon { get; set; }

	protected override void OnDisabled()
	{

	}

	protected override void OnDestroy()
	{

	}

	protected override void OnPreRender()
	{
		if ( !Player.IsLocalPlayer ) return;
		if ( Scene.Camera is null ) return;

		UpdateVignette();
		UpdateRadialIndicators();
	}

	void UpdateVignette()
	{

	}

	void UpdateRadialIndicators()
	{
		if ( RadialDamageIcon is null )
			return;

		var hud = Scene.Camera.Hud;
		var playerPos = Player.EyeTransform.Position;
		var playerRot = Player.EyeTransform.Rotation;
		var center = Screen.Size / 2f;

		// rough approx of where the crosshair is in worldspace, makes close-up directions more easily parsable/accurate
		var focalPoint = playerPos + playerRot.Forward * 16;

		for ( int i = radialIndicators.Count - 1; i >= 0; i-- )
		{
			var entry = radialIndicators[i];
			if ( entry.Lifetime >= RadialIndicatorLifetime )
			{
				radialIndicators.RemoveAt( i );
				continue;
			}

			var dir = (entry.WorldPos - focalPoint).Normal;
			var angle = -MathF.Atan2( dir.y, dir.x ) + playerRot.Angles().yaw.DegreeToRadian() - (MathF.PI / 2f);

			Matrix matrix = Matrix.CreateRotation( Rotation.From( 0, angle.RadianToDegree(), 0 ) );
			matrix *= Matrix.CreateTranslation( center );
			hud.SetMatrix( matrix );

			var size = new Vector2( 256, 512 ) * Hud.Scale;
			var rect = new Rect( new Vector2( RadialDistanceFromCenter * Hud.Scale, -size.y / 2 ), size );

			// scale alpha based on damage dealt or something?
			hud.DrawTexture( RadialDamageIcon, rect, Color.Red.WithAlpha( 1f - (entry.Lifetime / RadialIndicatorLifetime) ) );
		}

		hud.SetMatrix( Matrix.Identity );
	}

	void IPlayerEvent.OnDamage( IPlayerEvent.DamageParams args )
	{
		timeSinceHurt = 0f;

		if ( args.Attacker.IsValid() )
		{
			radialIndicators.Add( (args.Attacker.WorldPosition, 0f) );
		}
	}
}
