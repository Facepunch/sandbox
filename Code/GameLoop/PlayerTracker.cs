public class KillTracker : GameObjectSystem<KillTracker>
{
	private record KillRecord( float Time, string Weapon );

	private float KillWindow = 5f;
	private readonly Dictionary<Guid, List<KillRecord>> KillLog = new();

	public KillTracker( Scene scene ) : base( scene )
	{
	}

	public void OnPlayerKilled( PlayerData attacker, PlayerData victim, DeathmatchDamageInfo dmg )
	{
		if ( !attacker.IsValid() ) return;

		// Don't count suicide kills
		if ( attacker == victim ) return;

		var time = RealTime.Now;
		var id = attacker.PlayerId;
		var weaponName = dmg.Weapon.IsValid() ? dmg.Weapon.Name : "unknown";

		if ( !KillLog.ContainsKey( id ) )
		{
			KillLog[id] = new();
		}

		if ( KillLog[id].Any( kill => time - kill.Time > KillWindow ) )
		{
			Log.Trace( "Kill window is too high, clearing" );
			KillLog[id].Clear();
		}

		KillLog[id].Add( new KillRecord( time, weaponName ) );

		CheckForMultiKill( attacker, victim, weaponName );
	}

	void CheckForMultiKill( PlayerData attacker, PlayerData victim, string weapon )
	{
		var playerKills = KillLog[attacker.PlayerId];
		var totalKillCount = playerKills.Count;
		var weaponKillCount = playerKills.Count( kill => kill.Weapon == weapon );

		if ( totalKillCount >= 2 )
		{
			Log.Info( $"Multi Kill {attacker.DisplayName}! - {totalKillCount} kills" );
			attacker.AddStat( $"multikill.{totalKillCount}" );
		}

		Scene.RunEvent<Feed>( x => x.NotifyKill( attacker, totalKillCount ) );

		if ( weaponKillCount >= 2 )
		{
			attacker.AddStat( $"multikill.{weapon}.{weaponKillCount}" );
		}
	}
}
