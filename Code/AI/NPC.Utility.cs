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
	/// Calculates aim offset based on skill level and distance to target
	/// </summary>
	/// <returns>Modified aim position with skill-based inaccuracy</returns>
	private Vector3 CalculateAimVector( Vector3 targetPosition, float distance )
	{
		// Perfect aim (skill = 1.0) returns exact target position
		if ( AimingSkill >= 1f )
			return targetPosition;

		// Calculate maximum spread based in verse skill level
		// Lower skill = higher spread, distance also increases spread
		var maxSpread = (1f - AimingSkill) * 100f;
		var distanceMultiplier = distance / 1000f;
		var totalSpread = maxSpread * (1f + distanceMultiplier);

		// Add random offset in a circle around the target
		var randomAngle = Game.Random.Float( 0f, 360f );
		var randomDistance = Game.Random.Float( 0f, totalSpread );

		var offsetX = MathF.Cos( MathF.PI * randomAngle / 180f ) * randomDistance;
		var offsetY = MathF.Sin( MathF.PI * randomAngle / 180f ) * randomDistance;

		return targetPosition + new Vector3( offsetX, offsetY, 0f );
	}

	/// <summary>
	/// Create a ragdoll gameobject version of our render body.
	/// </summary>
	public GameObject CreateRagdoll( string name = "Ragdoll" )
	{
		var go = new GameObject( true, name );
		go.Tags.Add( "ragdoll" );
		go.WorldTransform = WorldTransform;

		var originalBody = Renderer.Components.Get<SkinnedModelRenderer>();

		if ( !originalBody.IsValid() )
			return go;

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

		return go;
	}
}
