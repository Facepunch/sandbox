using System.Threading;

namespace Sandbox.AI;

public sealed partial class Npc
{
	private IActor FindClosest( List<IActor> list )
	{
		return list
			.Where( v => v.IsValid() )
			.OrderBy( DistanceTo )
			.FirstOrDefault();
	}

	private IActor FindClosestWithinRange( List<IActor> list, float maxRange )
	{
		return list
			.Where( v => v.IsValid() )
			.Where( v => DistanceTo( v ) <= maxRange )
			.OrderBy( DistanceTo )
			.FirstOrDefault();
	}

	private float DistanceTo( IActor actor ) =>
		Vector3.DistanceBetween( WorldPosition, actor.WorldPosition );

	/// <summary>
	/// Gets the eye position of an actor for targeting
	/// </summary>
	private Vector3 GetEye( IActor actor )
	{
		return actor.EyeTransform.Position;
	}

	/// <summary>
	/// Check if the NPC has a usable weapon
	/// </summary>
	private bool HasWeapon()
	{
		return _weapon is BaseWeapon weapon && weapon.IsValid();
	}

	/// <summary>
	/// Calculates aim offset based on skill level, using angular deviation
	/// </summary>
	private Vector3 CalculateAimVector( Vector3 targetPosition, float distance )
	{
		// Perfect aim (skill = 1.0) returns exact target position
		if ( AimingSkill >= 1f )
			return targetPosition;

		var baseSpreadDegrees = 2f; // magic number 
		var skillMultiplier = (1f - AimingSkill) * 5f; // magic number 2

		// don't like all these magic numbers
		var distancePenalty = distance > 2048f ? 1f + ((distance - 2048f) / 4096f) : 1f;

		var maxSpreadDegrees = baseSpreadDegrees * skillMultiplier * distancePenalty;
		var randomAngle = Game.Random.Float( 0f, 360f );
		var randomSpread = Game.Random.Float( 0f, maxSpreadDegrees );
		var spreadRadians = randomSpread * (MathF.PI / 180f);
		var spreadRadius = MathF.Tan( spreadRadians ) * distance;

		// Apply the offset in a circle around the target
		var offsetX = MathF.Cos( MathF.PI * randomAngle / 180f ) * spreadRadius;
		var offsetY = MathF.Sin( MathF.PI * randomAngle / 180f ) * spreadRadius;

		return targetPosition + new Vector3( offsetX, offsetY, 0f );
	}

	/// <summary>
	/// Create a ragdoll gameobject version of our render body.
	/// </summary>
	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	public void CreateRagdoll()
	{
		var originalBody = Renderer.Components.Get<SkinnedModelRenderer>();

		if ( !originalBody.IsValid() )
			return;

		var go = new GameObject( true, "Ragdoll" );
		go.Tags.Add( "ragdoll" );
		go.WorldTransform = WorldTransform;

		var mainBody = go.Components.Create<SkinnedModelRenderer>();
		mainBody.CopyFrom( originalBody );
		mainBody.UseAnimGraph = false;

		// copy the clothes
		foreach ( var clothing in originalBody.GameObject.Children.SelectMany( x => x.Components.GetAll<SkinnedModelRenderer>() ) )
		{
			if ( !clothing.IsValid() ) continue;

			var newClothing = new GameObject( true, clothing.GameObject.Name );
			newClothing.Parent = go;

			var item = newClothing.Components.Create<SkinnedModelRenderer>();
			item.CopyFrom( clothing );
			item.BoneMergeTarget = mainBody;
		}

		var physics = go.Components.Create<ModelPhysics>();
		physics.Model = mainBody.Model;
		physics.Renderer = mainBody;
		physics.CopyBonesFrom( originalBody, true );
	}
}
