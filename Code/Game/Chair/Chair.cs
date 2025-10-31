
using Sandbox.Movement;
using Sandbox.Utility;

namespace Sandbox;


public partial class Chair : Component, Component.IPressable
{
	public enum AnimatorSitPose
	{
		Standing = 0,
		Sitting = 1,
		Kneeling = 2,
	}

	[Property] public AnimatorSitPose SitPose { get; set; } = AnimatorSitPose.Sitting;

	[Property] public GameObject SeatPosition { get; set; }

	[Property] public GameObject EyePosition { get; set; }
	[Property] public Vector2 PitchRange { get; set; } = new Vector2( -90, 70 );
	[Property] public Vector2 YawRange { get; set; } = new Vector2( -120, 120 );

	public bool CanPress( IPressable.Event e )
	{
		var player = e.Source as PlayerController;
		if ( player is null ) return false;
		return CanEnter( player );
	}

	public Transform enterEyes;

	public bool Press( IPressable.Event e )
	{
		var player = e.Source as PlayerController;
		if ( player is null ) return false;

		EnterChair( player );
		return true;
	}

	[Rpc.Host]
	private void EnterChair( PlayerController player )
	{
		if ( player.Network.Owner != Rpc.Caller )
			return;

		if ( !CanEnter( player ) ) return;

		using ( Rpc.FilterInclude( player.Network.Owner ) )
		{
			Sit( player );
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	public void Sit( PlayerController player )
	{
		var seatPos = SeatPosition ?? GameObject;

		enterEyes = player.EyeTransform;

		player.Body.Enabled = false;
		player.ColliderObject.Enabled = false;

		player.GameObject.SetParent( seatPos, false );
		player.GameObject.LocalTransform = global::Transform.Zero;
	}

	[Rpc.Host]
	public void AskToLeave( PlayerController player )
	{
		if ( player.Network.Owner != Rpc.Caller )
			return;

		using ( Rpc.FilterInclude( player.Network.Owner ) )
		{
			Eject( player );
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	public void Eject( PlayerController player )
	{
		var seatPos = SeatPosition ?? GameObject;

		player.GameObject.SetParent( null, true );
	}

	/// <summary>
	/// Return true if this player can enter the chair
	/// </summary>
	public virtual bool CanEnter( PlayerController player )
	{
		if ( player is null ) return false;
		if ( IsOccupied ) return false;

		return true;
	}

	public Transform GetEyeTransform()
	{
		var seatPos = EyePosition ?? SeatPosition ?? GameObject;
		return seatPos.WorldTransform;
	}

	public bool IsOccupied => GetComponentInChildren<PlayerController>() != null;
}

public class SittingMoveMode : MoveMode, PlayerController.IEvents
{
	TimeSince timesinceStarted;

	Transform originalEyes;

	public override int Score( PlayerController controller )
	{
		if ( controller.GetComponentInParent<Chair>() is Chair chair )
		{
			return 200000;
		}

		return -1;
	}

	public override void UpdateAnimator( SkinnedModelRenderer renderer )
	{
		if ( renderer.GetComponentInParent<Chair>() is not Chair chair )
			return;

		Controller.LocalTransform = global::Transform.Zero;

		renderer.LocalRotation = Rotation.Identity;
		renderer.Set( "sit", (int)chair.SitPose );
		renderer.Set( "b_grounded", true );
		renderer.Set( "b_climbing", false );
		renderer.Set( "b_swim", false );
		renderer.Set( "duck", false );

		OnUpdateAnimatorVelocity( renderer );
		OnUpdateAnimatorLookDirection( renderer );
	}

	public override void PrePhysicsStep()
	{

	}

	public override void OnModeBegin()
	{
		base.OnModeBegin();

		Controller.Body.Enabled = false;
		Controller.ColliderObject.Enabled = false;
		Controller.EyeAngles = default;

		timesinceStarted = 0;

		if ( GetComponentInParent<Chair>() is Chair chair )
		{
			originalEyes = chair.enterEyes;
		}
	}

	public override void OnModeEnd( MoveMode next )
	{
		Controller.Body.Enabled = true;
		Controller.ColliderObject.Enabled = true;

		Controller.WorldRotation = Rotation.LookAt( Controller.EyeTransform.Forward.WithZ( 0 ), Vector3.Up );

		base.OnModeEnd( next );
	}

	public override Vector3 UpdateMove( Rotation eyes, Vector3 input )
	{
		if ( GetComponentInParent<Chair>() is not Chair chair )
		{
			return 0;
		}

		ClampEyes( chair );

		return 0;
	}

	void ClampEyes( Chair chair )
	{
		var ea = Controller.EyeAngles;
		ea.pitch = MathX.Clamp( ea.pitch, chair.PitchRange.x, chair.PitchRange.y );
		ea.yaw = MathX.Clamp( ea.yaw, chair.YawRange.x, chair.YawRange.y );
		Controller.EyeAngles = ea;
	}

	public override Transform CalculateEyeTransform()
	{
		if ( GetComponentInParent<Chair>() is not Chair chair )
		{
			return base.CalculateEyeTransform();
		}

		ClampEyes( chair );

		var seatEyeTx = chair.GetEyeTransform();

		var transform = new Transform();
		transform.Position = seatEyeTx.Position;
		transform.Rotation = chair.WorldRotation * Controller.EyeAngles.ToRotation();

		const float lerpDuration = 0.2f;

		if ( timesinceStarted < lerpDuration )
		{
			float t = Easing.EaseOut( timesinceStarted / lerpDuration );
			transform.Position = Vector3.Lerp( originalEyes.Position, transform.Position, t );
			transform.Rotation = Rotation.Slerp( originalEyes.Rotation, transform.Rotation, t );
		}

		return transform;
	}

	void PlayerController.IEvents.FailPressing()
	{
		if ( GetComponentInParent<Chair>() is not Chair chair )
			return;

		chair.AskToLeave( Controller );
	}
}
