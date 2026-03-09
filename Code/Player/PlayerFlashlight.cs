/// <summary>
/// A Half-Life-style flashlight that the player can toggle with the Flashlight input action (F).
/// Add this component to a child GameObject of the player; attach a SpotLight to the same object.
/// </summary>
public sealed class PlayerFlashlight : Component
{
	[Property, RequireComponent] public SpotLight Light { get; set; }

	[Property, Group( "Sound" )] public SoundEvent ToggleOnSound { get; set; }
	[Property, Group( "Sound" )] public SoundEvent ToggleOffSound { get; set; }
	[Sync, Change( nameof(OnIsOnChanged) )] public bool IsOn { get; set; } = false;

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

	private void OnIsOnChanged() => UpdateLight();

	private void UpdateLight()
	{
		if ( Light.IsValid() )
			Light.Enabled = IsOn;
	}
}
