
using Sandbox.Utility;

public class HydraulicEntity : Component, IPlayerControllable
{
	[Property, Range( 0, 1 )]
	public GameObject OnEffect { get; set; }

	[Property, Range( 0, 100 ), ClientEditable]
	public float MinLength { get; set; } = 10f;

	[Property, Range( 0, 100 ), ClientEditable]
	public float MaxLength { get; set; } = 100f;

	[Property, Range( 0, 1 ), ClientEditable]
	public float Length { get; set; } = 0.5f;

	[Property, Range( 0, 1 ), ClientEditable]
	public float Speed { get; set; } = 0.25f;

	[Property, Sync, ClientEditable]
	public ClientInput Push { get; set; }

	[Property, Range( 0, 1 ), ClientEditable]
	public float PushSpeed { get; set; } = 0.25f;


	[Property, Sync, ClientEditable]
	public ClientInput Pull { get; set; }

	[Property, Range( 0, 1 ), ClientEditable]
	public float PullSpeed { get; set; } = 0.25f;

	[Property, Sync, ClientEditable]
	public ClientInput Toggle { get; set; }

	/// <summary>
	/// While the client input is active we'll apply thrust
	/// </summary>
	[Property, Sync, ClientEditable]
	public ClientInput Activate { get; set; }

	[Property]
	public SliderJoint Joint { get; set; }

	[Property, ClientEditable, ToggleGroup( "Animated" )]
	public bool Animated { get; set; }

	[Property, ClientEditable, ToggleGroup( "Animated" ), Range( 0, 10 )]
	public float AnimationSpeed { get; set; } = 1.0f;

	[Property, ClientEditable, ToggleGroup( "Animated" )]
	public EaseType EaseIn { get; set; } = EaseType.Linear;

	[Property, ClientEditable, ToggleGroup( "Animated" )]
	public EaseType EaseOut { get; set; } = EaseType.Linear;


	public enum EaseType
	{
		Linear,
		EaseIn,
		EaseOut,
		EaseInOut,
		Bounce
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		OnEffect?.Enabled = false;
	}

	bool _state;

	public void SetActiveState( bool state )
	{
		if ( _state == state ) return;

		_state = state;

		OnEffect?.Enabled = state;

		Network.Refresh();

	}

	public void OnStartControl()
	{
	}

	public void OnEndControl()
	{
	}

	float? _lastTargetValue;
	float? _targetValue;

	public void OnControl()
	{
		if ( Activate.Down() )
		{
			Length += Speed * Time.Delta;

		}
		else if ( Activate.Released() )
		{
			Length = 0;
		}

		if ( Push.Down() )
		{
			Length += PushSpeed * Time.Delta * 5.0f;
		}

		if ( Pull.Down() )
		{
			Length -= PullSpeed * Time.Delta * 5.0f;
		}

		if ( Toggle.Pressed() )
		{
			_targetValue = _lastTargetValue.HasValue ? (_lastTargetValue > 0.5f ? 0.0f : 1.0f) : 1;
			_lastTargetValue = _targetValue;
		}

		if ( _targetValue.HasValue )
		{
			if ( _targetValue > Length )
			{
				Length += PushSpeed * Time.Delta * 5.0f;

				if ( Length > 1 )
				{
					_targetValue = null;
				}
			}
			else
			{
				Length -= PullSpeed * Time.Delta * 5.0f;

				if ( Length < 0 )
				{
					_targetValue = null;
				}
			}
		}

		Length = Length.Clamp( 0, 1 );

		var analog = Activate.GetAnalog();
	}

	float _animTime = 0;

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( !Joint.IsValid() ) return;

		var line = Joint.Body.WorldPosition - Joint.GameObject.WorldPosition;
		var line_rot = Rotation.LookAt( line, WorldRotation.Up );

		DebugOverlay.Line( Joint.GameObject.WorldPosition, Joint.Body.WorldPosition, Color.Green );

		if ( Animated )
		{
			_animTime += Time.Delta * AnimationSpeed * 0.33f;
			_animTime = _animTime % 2;

			var delta = _animTime;
			if ( delta > 1 )
			{
				delta = 2 - delta;
				delta = GetEase( 1 - delta, EaseOut );
				delta = 1 - delta;
			}
			else
			{
				delta = GetEase( delta, EaseIn );
			}

			Length = (delta);
		}


		Joint.MinLength = MinLength + (Length * (MaxLength - MinLength));
		Joint.MaxLength = MinLength + (Length * (MaxLength - MinLength));

		if ( GetComponent<CapsuleCollider>() is CapsuleCollider capsule )
		{
			capsule.Static = false;
			capsule.Start = capsule.WorldTransform.PointToLocal( Joint.GameObject.WorldPosition );
			capsule.End = capsule.WorldTransform.PointToLocal( Joint.Body.WorldPosition );
			capsule.Radius = 1.0f;
			capsule.ColliderFlags = ColliderFlags.IgnoreMass;
			capsule.Tags.Set( "trigger", true );
		}

		if ( GetComponent<SkinnedModelRenderer>() is SkinnedModelRenderer renderer )
		{
			renderer.CreateBoneObjects = true;

			var len = line.Length - MinLength;

			var a = Joint.GameObject.WorldPosition;
			var b = a + line.Normal * MinLength * 0.5f;
			var c = b + line.Normal * len;
			var d = c + line.Normal * MinLength * 0.5f;

			renderer.GetBoneObject( 0 )?.WorldTransform = new Transform( a, line_rot );
			renderer.GetBoneObject( 0 )?.Flags |= GameObjectFlags.ProceduralBone;
			renderer.GetBoneObject( 1 )?.WorldTransform = new Transform( b, line_rot ); ;
			renderer.GetBoneObject( 1 )?.Flags |= GameObjectFlags.ProceduralBone;
			renderer.GetBoneObject( 2 )?.WorldTransform = new Transform( c, line_rot ); ;
			renderer.GetBoneObject( 2 )?.Flags |= GameObjectFlags.ProceduralBone;
			renderer.GetBoneObject( 3 )?.WorldTransform = new Transform( d, line_rot ); ;
			renderer.GetBoneObject( 3 )?.Flags |= GameObjectFlags.ProceduralBone;
		}

	}

	private float GetEase( float delta, EaseType easeIn )
	{
		switch ( easeIn )
		{
			case EaseType.Linear: return delta;
			case EaseType.EaseIn: return Easing.EaseIn( delta );
			case EaseType.EaseOut: return Easing.EaseOut( delta );
			case EaseType.EaseInOut: return Easing.EaseInOut( delta );
			case EaseType.Bounce: return Easing.BounceOut( delta );
		}

		return delta;
	}
}
