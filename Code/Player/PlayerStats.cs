/// <summary>
/// Record stats for the local player
/// </summary>
public sealed class PlayerStats : Component, IPlayerEvent
{
	[RequireComponent] public Player Player { get; set; }

	float metersTravelled;
	Vector3 lastPosition;

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		var delta = WorldPosition - lastPosition;
		lastPosition = WorldPosition;


		if ( !Player.Controller.IsOnGround )
		{
			return;
		}

		var groundDelta = delta.WithZ( 0 ).Length.InchToMeter();
		if ( groundDelta > 10 ) groundDelta = 0;

		metersTravelled += groundDelta;

		if ( metersTravelled > 10 )
		{
			Sandbox.Services.Stats.Increment( "meters_walked", metersTravelled );
			metersTravelled = 0;
		}

	}

	void IPlayerEvent.OnJump()
	{
		if ( IsProxy ) return;

		Sandbox.Services.Stats.Increment( "jump", 1 );
	}

	void IPlayerEvent.OnDamage( IPlayerEvent.DamageParams args )
	{
		if ( IsProxy ) return;

		Sandbox.Services.Stats.Increment( "damage_taken", args.Damage );
	}

	void IPlayerEvent.OnDied( IPlayerEvent.DiedParams args )
	{
		if ( IsProxy ) return;

		Sandbox.Services.Stats.Increment( "deaths", 1 );
	}

	void IPlayerEvent.OnSuicide()
	{
		if ( IsProxy ) return;

		Sandbox.Services.Stats.Increment( "suicides", 1 );
	}

}
