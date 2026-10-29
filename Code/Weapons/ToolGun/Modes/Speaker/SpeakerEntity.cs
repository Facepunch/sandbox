using Sandbox.UI;

/// <summary>
/// How a <see cref="SpeakerEntity"/> reacts to its key or trigger signal.
/// </summary>
public enum SpeakerMode
{
	/// <summary>
	/// Each press plays the sound once, all the way through.
	/// </summary>
	[Title( "#entity.speakermode.oneshot" )]
	OneShot,

	/// <summary>
	/// The sound plays while the input is held (repeating if it ends, with <see cref="SpeakerEntity.Looping"/>), and
	/// stops when it's let go.
	/// </summary>
	[Title( "#entity.speakermode.hold" )]
	Hold,

	/// <summary>
	/// Press once to start the sound, press again to stop it. Repeats while on with <see cref="SpeakerEntity.Looping"/>.
	/// </summary>
	[Title( "#entity.speakermode.toggle" )]
	Toggle,
}

/// <summary>
/// Plays a sound when told to: by a key from a linked seat, or by a wire - a button, a seat's Entered/Exited/Occupied
/// (see <see cref="SeatSignals"/>), logic, anything with an output.
///
/// All of the deciding happens on the host. Clients only hear the result: <see cref="IsOn"/> for held and toggled
/// sounds, a broadcast for one-shots.
/// </summary>
[Alias( "speaker" ), Title( "#entity.name.speaker" ), Icon( "volume_up" )]
public sealed class SpeakerEntity : Component, IPlayerControllable, Global.IControlEvents, IInspectHandle, ISignalDisplayName
{
	/// <summary>
	/// "Speaker: horn" in the wire tool, so several speakers on one contraption can be told apart.
	/// </summary>
	string ISignalDisplayName.SignalDisplayName
	{
		get
		{
			var title = Game.Language.GetPhrase( "entity.name.speaker" );
			var sound = SoundResolver.GetDisplayName( Sound );
			return sound is null ? title : $"{title}: {sound}";
		}
	}

	/// <summary>
	/// What a new speaker plays.
	/// </summary>
	public const string DefaultSound = "sounds/vehicles/buggy/horn.sound";

	/// <summary>
	/// The sound this speaker plays: any .sound in the game, or a workshop sound's package ident. Empty is silent.
	/// See <see cref="SoundResolver"/>.
	/// </summary>
	[Property, ClientEditable, ResourceSelect( Extension = "sound", AllowPackages = true, Popup = true ), Title( "#entity.property.speaker_sound" ), Description( "#entity.description.speaker_sound" ), Group( "#tool.setting.group.sound" ), Order( 0 )]
	public string Sound { get; set; } = DefaultSound;

	/// <summary>
	/// Hold and toggle: start the sound again whenever it ends, for as long as it's on. Off plays it once per
	/// press. Sounds that loop by themselves loop either way.
	/// </summary>
	[Property, ClientEditable, Title( "#entity.property.looping" ), Description( "#entity.description.speaker_looping" ), Group( "#tool.setting.group.sound" ), Order( 0 )]
	public bool Looping { get; set; } = false;

	/// <summary>
	/// One-shot, hold or toggle.
	/// </summary>
	[Property, ClientEditable, Title( "#entity.property.mode" ), Description( "#entity.description.speaker_mode" ), Group( "#tool.setting.group.behavior" ), Order( 0 )]
	public SpeakerMode Mode { get; set; } = SpeakerMode.OneShot;

	/// <summary>
	/// 0 to 1. Can be wired, e.g. to a throttle.
	/// </summary>
	[Property, Sync, ClientEditable, SignalInput, Range( 0, 1 ), Title( "#entity.property.volume" ), Description( "#entity.description.speaker_volume" ), Group( "#tool.setting.group.behavior" ), Order( 0 )]
	public float Volume { get; set; } = 1f;

	/// <summary>
	/// Playback speed, 1 is normal. Can be wired, e.g. to make an engine whine rise with speed.
	/// </summary>
	[Property, Sync, ClientEditable, SignalInput, Range( 0.25f, 2f ), Title( "#entity.property.pitch" ), Description( "#entity.description.speaker_pitch" ), Group( "#tool.setting.group.behavior" ), Order( 0 )]
	public float Pitch { get; set; } = 1f;

	/// <summary>
	/// The key that plays the sound for whoever sits in a seat on this contraption.
	/// </summary>
	[Property, ClientEditable, Title( "#entity.property.speaker_key" ), Description( "#entity.description.speaker_key" ), Group( "#tool.setting.group.inputs" ), Order( 1 )]
	public ClientInput Key { get; set; }

	/// <summary>
	/// Whether the held/toggled sound is playing. Owned by the host.
	/// </summary>
	[Sync] public bool IsOn { get; private set; }

	/// <summary>
	/// How long held and toggled sounds take to fade out when they stop.
	/// </summary>
	private const float FadeTime = 0.1f;

	// host state - the key and the wires are kept apart so letting go of one doesn't cancel the other
	private bool _keyOn;
	private bool _wireOn;
	private bool _wireDown;

	// client state
	private SoundHandle _loop;
	private SoundEvent _loopSound;
	private bool _playedThisTime;
	private SoundHandle _oneShot;

	protected override void OnEnabled()
	{
		InspectHandles.Register( this );
	}

	protected override void OnDisabled()
	{
		InspectHandles.Unregister( this );

		_keyOn = _wireOn = _wireDown = false;
		if ( !IsProxy ) IsOn = false;

		StopLoop( 0f );
		StopOneShot( 0f );
	}

	protected override void OnDestroy()
	{
		InspectHandles.Unregister( this );
		StopLoop( 0f );
		StopOneShot( 0f );
	}

	/// <summary>
	/// No model to click on, so the inspect menu shows an icon for it instead.
	/// </summary>
	bool IInspectHandle.ShowInspectHandle => !GetComponentInChildren<ModelRenderer>( true ).IsValid();

	#region Wire inputs

	/// <summary>
	/// Does whatever the key would: plays once, plays while held, or toggles, depending on the mode.
	/// Wire a button or a seat's Entered here and it behaves like the key.
	/// </summary>
	[SignalInput( Default = true, Name = "#entity.signal.speaker_trigger" )]
	public void Trigger( bool down )
	{
		var pressed = down && !_wireDown;
		_wireDown = down;

		switch ( Mode )
		{
			case SpeakerMode.OneShot:
				if ( pressed ) PlayOnce();
				break;
			case SpeakerMode.Toggle:
				if ( pressed ) SetWireOn( !_wireOn );
				break;
			case SpeakerMode.Hold:
				SetWireOn( down );
				break;
		}
	}

	/// <summary>
	/// Plays the sound once in one-shot mode, otherwise turns it on.
	/// </summary>
	[SignalInput( Name = "#entity.signal.speaker_start" )]
	public void StartSound()
	{
		if ( Mode == SpeakerMode.OneShot ) PlayOnce();
		else SetWireOn( true );
	}

	/// <summary>
	/// Stops whatever is playing, however it was started.
	/// </summary>
	[SignalInput( Name = "#entity.signal.speaker_stop" )]
	public void StopSound()
	{
		_keyOn = false;
		_wireOn = false;
		UpdateOn();
		StopAll();
	}

	#endregion

	#region Seat key

	/// <summary>
	/// The seated player's key. Runs on the host every fixed update while someone sits in a linked seat.
	/// </summary>
	public void OnControl()
	{
		switch ( Mode )
		{
			case SpeakerMode.OneShot:
				if ( Key.Pressed() ) PlayOnce();
				break;
			case SpeakerMode.Toggle:
				if ( Key.Pressed() ) SetKeyOn( !_keyOn );
				break;
			case SpeakerMode.Hold:
				SetKeyOn( Key.Down() );
				break;
		}
	}

	void Global.IControlEvents.OnControlStopped( ControlEvent e )
	{
		// OnControl stops being called once they're out, so nothing would ever let go of a held key
		if ( Mode != SpeakerMode.Hold || !_keyOn || !IsPartOf( e.Target ) ) return;

		SetKeyOn( false );
	}

	/// <summary>
	/// Is this seat (or driven prop) on the same contraption as us?
	/// </summary>
	private bool IsPartOf( GameObject target )
	{
		if ( !Networking.IsHost || !target.IsValid() ) return false;

		var linked = new LinkedGameObjectBuilder();
		linked.AddConnected( GameObject );
		return linked.Objects.Contains( target.Root );
	}

	#endregion

	#region Host state

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		// switched to one-shot while something was held or toggled on
		if ( Mode == SpeakerMode.OneShot && (_keyOn || _wireOn) )
		{
			_keyOn = _wireOn = false;
			UpdateOn();
		}
	}

	private void SetKeyOn( bool on )
	{
		if ( _keyOn == on ) return;
		_keyOn = on;
		UpdateOn();
	}

	private void SetWireOn( bool on )
	{
		if ( _wireOn == on ) return;
		_wireOn = on;
		UpdateOn();
	}

	private void UpdateOn()
	{
		if ( IsProxy ) return;
		IsOn = _keyOn || _wireOn;
	}

	#endregion

	#region Playback

	/// <summary>
	/// Play the sound once, for everyone.
	/// </summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void PlayOnce()
	{
		// a workshop sound that hasn't finished downloading yet is skipped - OnUpdate preloads it, so this is rare
		var sound = SoundResolver.Get( Sound );
		if ( sound is null ) return;

		var handle = PlayAttached( sound );
		if ( !handle.IsValid() ) return;

		handle.Volume = Volume;
		handle.Pitch = Pitch;

		StopOneShot( 0f );
		_oneShot = handle;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void StopAll()
	{
		StopOneShot( FadeTime );
		StopLoop( FadeTime );
	}

	private SoundHandle PlayAttached( SoundEvent sound )
	{
		var handle = Sandbox.Sound.Play( sound, WorldPosition );
		if ( handle.IsValid() )
		{
			handle.Parent = GameObject;
			handle.FollowParent = true;
		}

		return handle;
	}

	protected override void OnUpdate()
	{
		// also starts downloading a workshop pick, so it's ready before it's needed
		var sound = SoundResolver.Get( Sound );

		if ( IsOn && sound is not null )
		{
			// picked a different sound while it was playing - start the new one
			if ( _loopSound != sound )
			{
				StopLoop( 0f );
				_playedThisTime = false;
			}

			// starts once each time it's turned on; when looping, again every time it ends
			var ended = !_loop.IsValid() || _loop.IsStopped;
			if ( ended && (!_playedThisTime || Looping) )
			{
				_loop = PlayAttached( sound );
				_loopSound = sound;
				_playedThisTime = true;
			}
		}
		else
		{
			StopLoop( FadeTime );
			_playedThisTime = false;
		}

		if ( _loop.IsValid() )
		{
			_loop.Volume = Volume;
			_loop.Pitch = Pitch;
		}

		if ( _oneShot.IsValid() )
		{
			if ( _oneShot.IsStopped )
			{
				_oneShot = null;
			}
			else
			{
				_oneShot.Volume = Volume;
				_oneShot.Pitch = Pitch;
			}
		}
	}

	private void StopLoop( float fade )
	{
		if ( _loop.IsValid() )
			_loop.Stop( fade );

		_loop = null;
		_loopSound = null;
	}

	private void StopOneShot( float fade )
	{
		if ( _oneShot.IsValid() )
			_oneShot.Stop( fade );

		_oneShot = null;
	}

	#endregion
}
