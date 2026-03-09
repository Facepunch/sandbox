public sealed class PlayerFlashlight : Component
{
	[Property, RequireComponent] public SpotLight Light { get; set; }

	[Property, Group( "Sound" )] public SoundEvent ToggleOnSound { get; set; }
	[Property, Group( "Sound" )] public SoundEvent ToggleOffSound { get; set; }
	[Sync, Change( nameof( UpdateLight ) )] public bool IsOn { get; set; } = false;

	private Player _player;
	private Transform _localOffset;

	protected override void OnStart()
	{
		_player = GetComponentInParent<Player>();
		_localOffset = LocalTransform;
		UpdateLight();
	}

	protected override void OnUpdate()
	{
		if ( !_player.IsValid() ) return;

		if ( !IsProxy && Input.Pressed( "Flashlight" ) )
		{
			Toggle();
		}

		WorldTransform = _player.EyeTransform.ToWorld( _localOffset );
	}

	private void Toggle()
	{
		BroadcastToggle( !IsOn );
	}

	[Rpc.Broadcast]
	private void BroadcastToggle( bool value )
	{
		IsOn = value;

		var sound = IsOn ? ToggleOnSound : ToggleOffSound;
		if ( sound is not null )
			Sound.Play( sound, WorldPosition );
	}

	private void UpdateLight()
	{
		if ( !Light.IsValid() ) return;
		
		Light.Enabled = IsOn;
	}
}
