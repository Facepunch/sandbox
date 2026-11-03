using Sandbox.Npcs;

public enum CameraTrackingTarget
{
	[Title( "#entity.camera_tracking.owner" )] Owner,
	[Title( "#entity.camera_tracking.player" )] NearestPlayer,
	[Title( "#entity.camera_tracking.npc" )] NearestNpc,
	[Title( "#entity.camera_tracking.player_npc" )] NearestPlayerOrNpc
}

public sealed partial class CameraEntity
{
	[Property, ClientEditable, ToggleGroup( "Tracking", Label = "#entity.group.camera_tracking" )]
	public bool Tracking { get; set; }

	[Property, ClientEditable, Title( "#entity.property.camera_tracking_target" ), ToggleGroup( "Tracking", Label = "#entity.group.camera_tracking" )]
	public CameraTrackingTarget TrackingTarget { get; set; } = CameraTrackingTarget.Owner;

	[Property, ClientEditable, Range( 0.1f, 20f ), Title( "#entity.property.camera_tracking_speed" ), Description( "#entity.description.camera_tracking_speed" ), ToggleGroup( "Tracking", Label = "#entity.group.camera_tracking" )]
	public float TrackingLerpSpeed { get; set; } = 5f;

	[Property, ClientEditable, Range( 0f, 8192f ), Title( "#entity.property.camera_tracking_range" ), Description( "#entity.description.camera_tracking_range" ), ToggleGroup( "Tracking", Label = "#entity.group.camera_tracking" )]
	public float TrackingRange { get; set; } = 2048f;

	[Property, ClientEditable, Title( "#entity.property.camera_tracking_return" ), Description( "#entity.description.camera_tracking_return" ), ToggleGroup( "Tracking", Label = "#entity.group.camera_tracking" )]
	public bool ReturnToOriginal { get; set; } = true;

	Component _trackingTarget;
	Vector3 _smoothedTarget;
	Rotation _trackingRotation;
	Transform _trackingView;
	bool _trackingInitialized;
	RealTimeSince _sinceTargetSearch = 999f;
	CameraTrackingTarget _lastTrackingTarget;
	float _lastTrackingRange = -1;

	Transform PhysicalView => Feed.IsValid() ? Feed.PhysicalViewTransform : WorldTransform;

	void ResetTracking()
	{
		_trackingInitialized = false;
		_trackingTarget = null;
		_sinceTargetSearch = 999f;
		if ( Feed.IsValid() ) Feed.UseLensView( ClampedFov );
	}

	void UpdateTracking()
	{
		if ( !Tracking )
		{
			if ( _trackingInitialized ) ResetTracking();
			return;
		}

		var mount = PhysicalView;
		float range = float.IsFinite( TrackingRange ) ? TrackingRange.Clamp( 0f, 8192f ) : 2048f;
		float speed = float.IsFinite( TrackingLerpSpeed ) ? TrackingLerpSpeed.Clamp( 0.1f, 20f ) : 5f;
		float blend = 1f - MathF.Exp( -speed * MathF.Max( Time.Delta, 0f ) );

		bool hasTarget = TryTrackingPoint( _trackingTarget, out var targetPoint )
			&& (targetPoint - mount.Position).LengthSquared <= range * range;
		// Keep following the selected actor every frame, but avoid a full scene scan
		// every frame for every camera. Lost/out-of-range targets are replaced immediately.
		if ( _sinceTargetSearch >= 0.15f || _lastTrackingTarget != TrackingTarget || _lastTrackingRange != range
			|| _trackingTarget is not null && !hasTarget )
		{
			_trackingTarget = FindTrackingTarget( mount.Position, range );
			_sinceTargetSearch = 0;
			_lastTrackingTarget = TrackingTarget;
			_lastTrackingRange = range;
			hasTarget = TryTrackingPoint( _trackingTarget, out targetPoint );
		}

		if ( !_trackingInitialized )
		{
			_trackingRotation = mount.Rotation;
			float distance = hasTarget ? MathF.Max( (targetPoint - mount.Position).Length, 32f ) : 256f;
			_smoothedTarget = mount.Position + mount.Forward * distance;
			_trackingInitialized = true;
		}

		if ( hasTarget )
		{
			// Deliberately retain this point across target changes instead of snapping to
			// the newly chosen actor. Exponential interpolation is frame-rate independent.
			_smoothedTarget = _smoothedTarget.LerpTo( targetPoint, blend );
			var direction = _smoothedTarget - mount.Position;
			if ( direction.LengthSquared > 0.01f )
				_trackingRotation = Rotation.Slerp( _trackingRotation, Rotation.LookAt( direction.Normal ), blend );
		}
		else
		{
			if ( ReturnToOriginal )
				_trackingRotation = Rotation.Slerp( _trackingRotation, mount.Rotation, blend );
			// Reacquisition starts from the current aim, including while returning home.
			float distance = MathF.Max( (_smoothedTarget - mount.Position).Length, 32f );
			_smoothedTarget = mount.Position + _trackingRotation.Forward * distance;
		}

		_trackingView = new Transform( mount.Position, _trackingRotation );
	}

	Component FindTrackingTarget( Vector3 origin, float range )
	{
		Component nearest = null;
		float closest = range * range;
		void Consider( Component candidate )
		{
			if ( !TryTrackingPoint( candidate, out var point ) ) return;
			float distance = (point - origin).LengthSquared;
			if ( distance > closest || nearest is not null && distance == closest ) return;
			closest = distance;
			nearest = candidate;
		}

		if ( TrackingTarget == CameraTrackingTarget.Owner )
		{
			if ( GetComponent<Ownable>()?.Owner is { } owner ) Consider( Player.FindForConnection( owner ) );
			return nearest;
		}
		if ( TrackingTarget is CameraTrackingTarget.NearestPlayer or CameraTrackingTarget.NearestPlayerOrNpc )
			foreach ( var player in Scene.GetAllComponents<Player>() ) Consider( player );
		if ( TrackingTarget is CameraTrackingTarget.NearestNpc or CameraTrackingTarget.NearestPlayerOrNpc )
			foreach ( var npc in Scene.GetAllComponents<Npc>() ) Consider( npc );
		return nearest;
	}

	bool TryTrackingPoint( Component target, out Vector3 point )
	{
		point = default;
		if ( !target.IsValid() || !target.Active || target.Scene != Scene ) return false;
		switch ( target )
		{
			case Player player when player.Health > 0 && player.Controller.IsValid():
				// Track the player's physical eyes, never their remote camera view.
				point = player.EyeTransform.Position;
				break;
			case Npc npc when npc.Health > 0:
				if ( npc.Renderer.IsValid() && npc.Renderer.GetAttachment( "eyes" ) is { } eyes )
					point = eyes.Position;
				else if ( npc.Renderer.IsValid() && npc.Renderer.Model is { } model && model.Bounds.Size.IsFinite )
					point = npc.Renderer.WorldTransform.PointToWorld( model.Bounds.Center );
				else point = npc.WorldPosition;
				break;
			default: return false;
		}
		return point.IsFinite;
	}
}
