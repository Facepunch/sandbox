using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.CombatNpc;

/// <summary>
/// Friendly NPC schedule: follow the nearest player, staying within follow distance.
/// Cancels when the NPC spots a hostile target.
/// </summary>
public class CombatFollowSchedule : ScheduleBase
{
	private static readonly string[] FollowLines =
	{
		"Right behind you.",
		"I've got your back.",
		"Lead the way.",
		"Sticking with you.",
		"On your six.",
	};

	/// <summary>
	/// How close the NPC tries to stay to the player.
	/// </summary>
	public float FollowDistance { get; set; } = 150f;

	private GameObject _followTarget;

	protected override void OnStart()
	{
		_followTarget = FindNearestPlayer();

		if ( !_followTarget.IsValid() )
		{
			AddTask( new Wait( 2f ) );
			return;
		}

		Npc.Animation.SetLookTarget( _followTarget );

		if ( Npc.Speech.CanSpeak && Game.Random.Float() < 0.15f )
			AddTask( new Say( Game.Random.FromArray( FollowLines ), 2f ) );

		AddTask( new MoveTo( _followTarget, FollowDistance ) );
		AddTask( new Wait( Game.Random.Float( 0.5f, 1.5f ) ) );
	}

	protected override void OnEnd()
	{
		Npc.Animation.ClearLookTarget();
	}

	protected override bool ShouldCancel()
	{
		return Npc.Senses.GetNearestVisible().IsValid();
	}

	private GameObject FindNearestPlayer()
	{
		GameObject nearest = null;
		float nearestDist = float.MaxValue;

		foreach ( var obj in Npc.Scene.FindInPhysics( new Sphere( Npc.WorldPosition, Npc.Senses.SightRange ) ) )
		{
			if ( !obj.Tags.Has( "player" ) ) continue;

			var dist = Npc.WorldPosition.Distance( obj.WorldPosition );
			if ( dist < nearestDist )
			{
				nearestDist = dist;
				nearest = obj;
			}
		}

		return nearest;
	}
}
