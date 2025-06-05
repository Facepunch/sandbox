/// <summary>
/// A charge station. It can either heal health, or armour.
/// </summary>
public sealed class ChargeStation : Component, Component.IPressable
{
	/// <summary>
	/// The default / maximum charger power.
	/// </summary>
	[Property, Title( "Charger Power" )]
	public float DefaultChargerPower { get; set; } = 50f;

	/// <summary>
	/// A synced representation of how much power is in the charge station.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public float ChargerPower { get; set; }

	/// <summary>
	/// How long without use until the charge station resets
	/// </summary>
	[Property, Title( "Charger Reset Time" )]
	public float ChargerResetTime { get; set; } = 60f;

	/// <summary>
	/// Should this charger charge armour or health?
	/// </summary>
	[Property, Title( "Is Armour Charger" )]
	public bool IsArmourCharger { get; set; } = false;

	/// <summary>
	/// How fast should we drain this charger
	/// </summary>
	[Property]
	public float AddSpeed { get; set; } = 10f;

	/// <summary>
	/// The sound to play when we are charging (loop sound)
	/// </summary>
	[Property]
	public SoundEvent ChargeSound { get; set; }

	/// <summary>
	/// Text to show the current charger power
	/// </summary>
	[Property]
	TextRenderer TextRenderer { get; set; }

	/// <summary>
	/// The player who is using the charger right now
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	private Player Player { get; set; }

	private TimeSince TimeSinceUsed;
	private SoundHandle loopSound;
	private bool wasCharging;

	protected override void OnStart()
	{
		ChargerPower = DefaultChargerPower;
	}

	protected override void OnUpdate()
	{
		TextRenderer.Text = $"Charger Power: {(int)ChargerPower}";

		var isCharging = Player.IsValid() && ChargerPower >= 1;

		// Start or update sound if charging
		if ( isCharging )
		{
			if ( !loopSound.IsValid() && ChargeSound is not null )
			{
				loopSound = Sound.Play( ChargeSound, WorldPosition );
			}

			if ( loopSound.IsValid() )
			{
				loopSound.Pitch = 0.6f + 1 - (ChargerPower / 50f * 0.8f);
			}
		}
		else if ( wasCharging )
		{
			if ( loopSound.IsValid() )
			{
				loopSound.Stop();
				loopSound = null;
			}

			// Play a sound to say we stopped using this
			Sound.Play( "items/chargestations/accept_charge.sound", WorldPosition );
		}

		wasCharging = isCharging;

		if ( IsProxy )
			return;

		if ( TimeSinceUsed > ChargerResetTime )
		{
			ChargerPower = DefaultChargerPower;
		}

		if ( isCharging && ShouldCharge( Player ) )
		{
			if ( IsArmourCharger )
			{
				ChargePlayer();
			}
			else
			{
				HealPlayer();
			}
		}
	}

	bool IPressable.Press( IPressable.Event e )
	{
		if ( ChargerPower < 1 )
			return false;

		var player = e.Source.GameObject.GetComponent<Player>();
		if ( !player.IsValid() )
			return false;

		StartChargingRpc( player );

		return true;
	}

	void IPressable.Release( IPressable.Event e )
	{
		StopChargingRpc();
	}

	bool IPressable.Pressing( IPressable.Event e )
	{
		return ChargerPower >= 1;
	}

	[Rpc.Host]
	private void StartChargingRpc( Player player )
	{
		if ( !player.IsValid() )
			return;

		if ( !ShouldCharge( player ) )
		{
			PlayRejectSound();

			return;
		}

		Player = player;
	}

	[Rpc.Broadcast]
	private void PlayRejectSound()
	{
		Sound.Play( "items/chargestations/deny_charge.sound", WorldPosition );
	}

	[Rpc.Host]
	private void StopChargingRpc()
	{
		if ( !Player.IsValid() )
			return;

		Player = null;
	}

	bool DrainPower( float amt )
	{
		if ( (ChargerPower - amt) <= 0f )
			return false;

		ChargerPower -= amt;
		TimeSinceUsed = 0;
		return true;
	}

	private bool ShouldCharge( Player player )
	{
		if ( !player.IsValid() )
			return false;

		// Health check
		if ( !IsArmourCharger && player.Health >= player.MaxHealth )
			return false;

		// Armour check
		if ( IsArmourCharger && player.Armour >= player.MaxArmour )
			return false;

		return true;
	}

	void HealPlayer()
	{
		if ( !ShouldCharge( Player ) )
		{
			StopChargingRpc();
			return;
		}

		var add = AddSpeed * Time.Delta;
		if ( DrainPower( add ) )
		{
			Player.Health = (Player.Health + add).Clamp( 0, Player.MaxHealth );
		}
		else
		{
			StopChargingRpc();
		}
	}

	void ChargePlayer()
	{
		if ( !ShouldCharge( Player ) )
		{
			StopChargingRpc();
			return;
		}

		var add = AddSpeed * Time.Delta;
		if ( DrainPower( add ) )
		{
			Player.Armour = (Player.Armour + add).Clamp( 0, Player.MaxArmour );
		}
		else
		{
			StopChargingRpc();
		}
	}
}
