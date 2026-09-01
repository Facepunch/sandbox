/// <summary>
/// Whether the emitter fires while the input is held, or toggles on/off with a press.
/// </summary>
public enum EmitMode
{
	/// <summary>
	/// Press once to turn on, press again to turn off.
	/// </summary>
	Toggle,
	/// <summary>
	/// Emits only while the input is held down.
	/// </summary>
	Hold,
}

/// <summary>
/// A world-placed SENT that spawns and controls a particle/VFX emitter.
/// The emitter prefab is defined by a <see cref="ScriptedEmitter"/> resource.
/// </summary>
[Alias( "emitter" )]
public sealed class EmitterEntity : Component, IPlayerControllable
{
	/// <summary>
	/// The emitter definition points to a prefab containing a particle system.
	/// </summary>
	[Property, ClientEditable]
	public ScriptedEmitter Emitter { get; set; }

	/// <summary>
	/// Whether this emitter toggles on/off with a press, or emits only while held.
	/// </summary>
	[Property, ClientEditable]
	public EmitMode Mode { get; set; } = EmitMode.Toggle;

	/// <summary>
	/// Used when <see cref="Mode"/> is <see cref="EmitMode.Toggle"/>.
	/// </summary>
	[Property, ClientEditable, Group( "Input" )]
	public ClientInput ToggleInput { get; set; }

	/// <summary>
	/// Used when <see cref="Mode"/> is <see cref="EmitMode.Hold"/>.
	/// </summary>
	[Property, ClientEditable, Group( "Input" )]
	public ClientInput HoldInput { get; set; }

	/// <summary>
	/// Whether the emitter is currently active. Synced to all clients.
	/// </summary>
	[Sync] public bool IsEmitting { get; private set; }

	/// <summary>
	/// When enabled, forces the emitter on regardless of input or mode.
	/// Can be set from the editor or wired up externally.
	/// </summary>
	[Property, ClientEditable, SignalInput( Default = true )]
	public bool ManualOn
	{
		get => _manualOn;
		set { _manualOn = value; if ( !IsProxy ) UpdateEmitState(); }
	}
	private bool _manualOn;
	private bool _inputEmitting;

	private GameObject _particleInstance;
	private ScriptedEmitter _lastEmitter;

	protected override void OnStart() { }

	protected override void OnUpdate()
	{
		// Emitter resource changed — destroy existing instance so it gets recreated
		if ( _lastEmitter != Emitter && _particleInstance.IsValid() )
			DestroyParticle();

		_lastEmitter = Emitter;

		if ( IsEmitting && !_particleInstance.IsValid() )
			SpawnParticle();
		else if ( !IsEmitting && _particleInstance.IsValid() )
			DestroyParticle();
	}

	[SignalInput( Id = nameof( ToggleInput ) )]
	public void ToggleSignal()
	{
		if ( Mode == EmitMode.Toggle ) ToggleEmitting();
	}

	[SignalInput( Id = nameof( HoldInput ) )]
	public void HoldSignal( bool active )
	{
		if ( Mode == EmitMode.Hold ) SetInputEmitting( active );
	}

	public void OnControl()
	{
		if ( Mode == EmitMode.Toggle )
		{
			if ( ToggleInput.Pressed() ) ToggleEmitting();
		}
		else
		{
			SetInputEmitting( HoldInput.Down() );
		}
	}

	private void ToggleEmitting() => SetInputEmitting( !_inputEmitting );

	private void SetInputEmitting( bool active )
	{
		if ( active == _inputEmitting ) return;
		_inputEmitting = active;
		UpdateEmitState();
	}

	private void UpdateEmitState() => SetEmitting( _inputEmitting || _manualOn );

	[Rpc.Broadcast]
	private void SetEmitting( bool active )
	{
		IsEmitting = active;
	}

	private void SpawnParticle()
	{
		if ( !Emitter.IsValid() || Emitter.Prefab is null ) return;

		_particleInstance = GameObject.Clone( Emitter.Prefab, new CloneConfig
		{
			Parent = GameObject,
			Transform = new Transform( Vector3.Forward * 4f ),
			StartEnabled = true,
		} );
	}

	private void DestroyParticle()
	{
		_particleInstance.Destroy();
		_particleInstance = null;
	}
}
