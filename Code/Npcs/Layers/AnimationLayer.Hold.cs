using Sandbox.Citizen;

namespace Sandbox.Npcs.Layers;

public sealed partial class AnimationLayer
{
	/// <summary>
	/// The prop currently being held, if any.
	/// </summary>
	public GameObject HeldProp => _heldProp;

	private GameObject _heldProp;
	private float _holdPose;
	private bool _oneHanded;

	/// <summary>
	/// Pick up and hold a prop — disables physics
	///
	/// holdtype_pose ranges:
	///   0-2 : close grip (~16u out), interpolates weight poses — used for heavy objects, but small enough to hold close
	///   2-4 : arms extend outwards — used for normal objects, mapped by width
	///   4-5 : above the head — used for large objects
	/// </summary>
	public void SetHeldProp( GameObject prop )
	{
		if ( !prop.IsValid() ) return;

		_heldProp = prop;

		// Grab mass before disabling physics
		var rb = prop.GetComponent<Rigidbody>( true );
		var mass = rb?.Mass ?? 1f;

		if ( rb.IsValid() )
			rb.Enabled = false;

		// Measure the object
		var bounds = prop.GetBounds();
		var size = bounds.Size;
		var width = MathF.Max( size.x, size.y );
		var diagonal = size.Length;

		// Determine pose and hold offset from object properties
		Vector3 holdOffset;
		var holdRotation = Npc.WorldRotation;

		// Small, light objects can be held one-handed
		_oneHanded = diagonal < 32f && mass <= 128;

		// TODO: too many magic numbers 

		if ( diagonal >= 64f )
		{
			// Large — above the citizen's head (pose 4-5)
			var t = ((diagonal - 64f) / 64f).Clamp( 0f, 1f );
			_holdPose = 4f + t;
			holdOffset = Vector3.Up * 66f + Npc.WorldRotation.Forward * 4f;

			// Orient the long axis forward so it doesn't stick out sideways
			prop.WorldRotation = holdRotation;
			var heldSize = prop.GetBounds().Size;
			var left = holdRotation.Left;
			var fwd = holdRotation.Forward;
			var sideExtent = MathF.Abs( heldSize.x * left.x ) + MathF.Abs( heldSize.y * left.y );
			var fwdExtent = MathF.Abs( heldSize.x * fwd.x ) + MathF.Abs( heldSize.y * fwd.y );

			if ( sideExtent > fwdExtent * 1.2f )
			{
				holdRotation *= Rotation.FromAxis( Vector3.Up, 90f );
			}
		}
		else if ( mass > 128 )
		{
			// Heavy — close grip (pose 0-2)
			var t = ((mass - 30f) / 170f).Clamp( 0f, 1f );
			_holdPose = t * 2f;
			holdOffset = Npc.WorldRotation.Forward * 8f + Vector3.Up * 30f;
		}
		else
		{
			// Normal — arms extend by width (pose 2-4, distance 16-32)
			var t = (width / 32f).Clamp( 0f, 1f );
			_holdPose = 2f + t * 2f;
			var forwardDist = 8 + t * 8f;
			holdOffset = Npc.WorldRotation.Forward * forwardDist + Vector3.Up * 30f;
		}

		// One-handed: parent directly to the right hand bone
		// Two-handed: parent to spine so it sways with the walk cycle
		GameObject parent;

		if ( _oneHanded )
		{
			var handBone = _renderer?.GetBoneObject( "hold_R" );
			parent = handBone ?? Npc.GameObject;

			prop.WorldPosition = parent.WorldPosition;
			prop.WorldRotation = holdRotation;
			prop.SetParent( parent, true );
		}
		else
		{
			var bone = _renderer?.GetBoneObject( "spine_2" );
			parent = bone ?? Npc.GameObject;

			prop.WorldPosition = Npc.WorldPosition + holdOffset;
			prop.WorldRotation = holdRotation;
			prop.SetParent( parent, true );
		}

		_renderer?.Set( "holdtype", (int)CitizenAnimationHelper.HoldTypes.HoldItem );
		_renderer?.Set( "holdtype_pose", _holdPose );
		_renderer?.Set( "holdtype_pose_hand", 0.005f );
		_renderer?.Set( "holdtype_handedness",
			(int)(_oneHanded ? CitizenAnimationHelper.Hand.Right : CitizenAnimationHelper.Hand.Left) );
	}

	/// <summary>
	/// Drop the held prop — clears IK, holdtype, holdtype_pose, places the prop
	/// on the ground in front of the NPC, unparents, and re-enables physics.
	/// Safe to call when nothing is held.
	/// </summary>
	public void ClearHeldProp()
	{
		if ( _renderer is not null )
		{
			_renderer.Set( "holdtype", 0 );
			_renderer.Set( "holdtype_pose", 0f );
			_renderer.Set( "holdtype_handedness", 0 );
			_renderer.ClearIk( "hand_right" );
			_renderer.ClearIk( "hand_left" );
		}

		if ( _heldProp.IsValid() )
		{
			// Use the prop's forward extent + padding so it lands clear of the Npc
			var bounds = _heldProp.GetBounds();
			var fwd = Npc.WorldRotation.Forward;
			var forwardExtent = MathF.Abs( bounds.Extents.x * fwd.x )
			                    + MathF.Abs( bounds.Extents.y * fwd.y );
			var dropDist = forwardExtent + 12f;

			var dropPos = Npc.WorldPosition
			              + fwd * dropDist
			              + Vector3.Up * bounds.Extents.z;

			_heldProp.WorldPosition = dropPos;
			_heldProp.WorldRotation = Npc.WorldRotation;
			_heldProp.SetParent( null, true );

			if ( _heldProp.GetComponent<Rigidbody>( true ) is { } rb )
				rb.Enabled = true;
		}

		_heldProp = null;
		_holdPose = 0f;
		_oneHanded = false;
	}

	/// <summary>
	/// Update IK hand targets each frame from the held prop's bounds.
	/// If the object is too wide to grip from the sides, support from below with palms up.
	/// </summary>
	private void UpdateHeldPropIk()
	{
		if ( _renderer is null || !_heldProp.IsValid() ) return;

		if ( _oneHanded )
		{
			_renderer.ClearIk( "hand_right" );
			_renderer.ClearIk( "hand_left" );
			return;
		}

		var bounds = _heldProp.GetBounds();
		var center = bounds.Center;
		var forward = _heldProp.WorldRotation.Forward;
		var left = _heldProp.WorldRotation.Left;

		var halfSpread = MathF.Max( MathF.Max( bounds.Extents.x, bounds.Extents.y ), 12f );

		Rotation rightRot;
		Rotation leftRot;
		Vector3 rightHandPos;
		Vector3 leftHandPos;

		if ( halfSpread > 24 )
		{
			// Too wide to grip from the sides — support from below, palms up
			rightHandPos = center - left * halfSpread + Vector3.Down * bounds.Extents.z;
			leftHandPos = center + left * halfSpread + Vector3.Down * bounds.Extents.z;

			rightRot = Rotation.LookAt( forward, Vector3.Down );
			leftRot = Rotation.LookAt( forward, Vector3.Up );
		}
		else
		{
			// Narrow enough to grip from the sides, palms inward
			rightHandPos = center - left * halfSpread;
			leftHandPos = center + left * halfSpread;

			rightRot = Rotation.LookAt( forward, -left );
			leftRot = Rotation.LookAt( forward, -left );
		}

		_renderer.SetIk( "hand_right", new Transform( rightHandPos, rightRot ) );
		_renderer.SetIk( "hand_left", new Transform( leftHandPos, leftRot ) );
	}
}
