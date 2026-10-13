using System.Threading;
using Sandbox.Npcs;

namespace Sandbox.Bots;

/// <summary>
/// Drives one player like a chaotic sandbox player: walks around, spawns things, shoots people,
/// uses the toolgun and physgun. It presses buttons through <see cref="BotInput"/> so the real
/// weapon and tool code runs, and drives a <see cref="Sandbox.Bot"/> or the local player.
/// </summary>
public sealed class BotBrain
{
	public Bot Bot { get; }
	public Connection Connection => Bot?.Connection;
	public string Name => Bot?.Name ?? "local";
	public string CurrentAction { get; private set; } = "idle";

	readonly BotInput _input;
	CancellationTokenSource _cts = new();
	Task _run;
	// Real time, Run resumes outside the scene so Time.Now differs from Tick
	RealTimeSince _timeSinceLoop;
	Player _player;
	Player _armedPlayer;

	// Aim and movement intents, applied every frame
	Vector3? _lookAt;
	GameObject _lookAtObject;
	Vector3 _lookAtOffset;
	Vector3? _moveTo;
	bool _sprint;
	float _turnSpeed = 360f;
	Vector3 _lastPosition;
	RealTimeSince _timeSinceMoved;

	// Most players fly around in noclip, it's the easiest way to get anywhere
	bool _wantNoclip = Random.Shared.Float() < 0.75f;
	RealTimeSince _timeSinceNoclipToggle;

	public BotBrain( Bot bot )
	{
		Bot = bot;
		_input = new BotInput( bot );
	}

	bool IsBot => Bot is not null;

	public void Stop()
	{
		_cts.Cancel();
		_input.ReleaseAll();
	}

	Player FindPlayer()
	{
		if ( Connection is null ) return Player.FindLocalPlayer();
		return Player.FindForConnection( Connection );
	}

	/// <summary>
	/// Called every frame before the player controllers update.
	/// </summary>
	public void Tick()
	{
		var player = FindPlayer();
		if ( player != _player )
		{
			_player = player;
			_input.ReleaseAll();
		}

		// Game tasks are dropped when their context expires, e.g. on hotload, so restart a loop that stopped
		if ( _run is null || _run.IsCompleted || _timeSinceLoop > 30 )
		{
			if ( _run is { IsFaulted: true } ) Log.Warning( $"[bot] {Name} loop: {_run.Exception}" );
			_cts.Cancel();
			_cts = new();
			_timeSinceLoop = 0;
			_run = Run( _cts.Token );
		}

		if ( _player.IsValid() && _player.Controller.IsValid() )
		{
			if ( _player.IsNoclipping != _wantNoclip && _timeSinceNoclipToggle > 1 )
			{
				_input.Tap( "noclip" );
				_timeSinceNoclipToggle = 0;
			}

			UpdateLook( _player.Controller );
			UpdateMove( _player.Controller );
		}

		_input.Frame();
	}

	/// <summary>
	/// Called at the start of every fixed update.
	/// </summary>
	public void FixedUpdate() => _input.FixedUpdate();

	void UpdateLook( PlayerController controller )
	{
		Vector3? target = _lookAtObject.IsValid() ? _lookAtObject.WorldPosition + _lookAtOffset : _lookAt;
		if ( target is null )
		{
			_input.Look( default );
			return;
		}

		var eye = controller.EyeTransform.Position;
		var desired = Rotation.LookAt( target.Value - eye ).Angles();
		var current = controller.EyeAngles;

		var delta = new Angles(
			MathX.DeltaDegrees( current.pitch, desired.pitch ),
			MathX.DeltaDegrees( current.yaw, desired.yaw ),
			0 );

		var max = _turnSpeed * Time.Delta;
		delta.pitch = delta.pitch.Clamp( -max, max );
		delta.yaw = delta.yaw.Clamp( -max, max );

		// A tiny bit of human wobble
		delta += new Angles( Random.Shared.Float( -0.3f, 0.3f ), Random.Shared.Float( -0.3f, 0.3f ), 0 );

		_input.Look( delta );
	}

	void UpdateMove( PlayerController controller )
	{
		var pos = controller.WorldPosition;
		if ( (pos - _lastPosition).Length > 4 )
		{
			_lastPosition = pos;
			_timeSinceMoved = 0;
		}

		if ( _moveTo is null )
		{
			_input.AnalogMove = default;
			_input.SetAction( "run", false );
			return;
		}

		var flying = _player.IsNoclipping;
		var to = _moveTo.Value - pos;
		if ( !flying ) to = to.WithZ( 0 );

		if ( to.Length < 48 )
		{
			_moveTo = null;
			_input.AnalogMove = default;
			return;
		}

		var basis = flying ? controller.EyeAngles.ToRotation() : Rotation.FromYaw( controller.EyeAngles.yaw );
		var local = basis.Inverse * to.Normal;
		_input.AnalogMove = new Vector3( local.x, local.y, flying ? local.z : 0 );
		_input.SetAction( "run", _sprint );

		// Stuck? hop
		if ( _timeSinceMoved > 1.0f )
		{
			_input.Tap( "jump" );
			_timeSinceMoved = 0;
			if ( Random.Shared.Float() < 0.3f ) _moveTo = null;
		}
	}

	//
	// Behaviour loop
	//

	async Task Run( CancellationToken ct )
	{
		await GameTask.DelaySeconds( Random.Shared.Float( 0.5f, 2f ) );

		while ( !ct.IsCancellationRequested )
		{
			_timeSinceLoop = 0;

			if ( !_player.IsValid() )
			{
				CurrentAction = "dead";
				await GameTask.DelaySeconds( 0.5f );
				continue;
			}

			if ( _player != _armedPlayer && BotContent.Weapons.Count > 0 )
			{
				_armedPlayer = _player;
				CurrentAction = "arm";
				await Arm( ct );
			}

			var action = BotSystem.PickAction();
			CurrentAction = action;

			try
			{
				await RunAction( action, ct );
			}
			catch ( OperationCanceledException ) { return; }
			catch ( Exception e )
			{
				Log.Warning( $"[bot] {Name} {action}: {e}" );
			}

			ReleaseHands();
			await GameTask.DelaySeconds( Random.Shared.Float( 0.1f, 0.8f ) );
		}
	}

	// Bots don't restore a saved hotbar, so each life spawns a few guns
	async Task Arm( CancellationToken ct )
	{
		var count = Random.Shared.Int( 2, 4 );
		for ( int i = 0; i < count; i++ )
		{
			Spawn( BotContent.Pick( BotContent.Weapons ) );
			await Wait( 0.3f, ct );
		}
	}

	void ReleaseHands()
	{
		foreach ( var a in (ReadOnlySpan<string>)["attack1", "attack2", "reload", "use", "duck", "walk"] )
			_input.SetAction( a, false );
	}

	Task RunAction( string action, CancellationToken ct ) => action switch
	{
		"wander" => Wander( ct ),
		"noclip" => ToggleNoclip( ct ),
		"spawn_prop" => SpawnSomething( Random.Shared.Float() < 0.4f && BotContent.Featured.Count > 0 ? BotContent.Featured : BotContent.Props, ct ),
		"spawn_entity" => SpawnSomething( BotContent.Entities, ct ),
		"spawn_npc" => SpawnSomething( BotContent.Npcs, ct ),
		"spawn_dupe" => SpawnSomething( BotContent.Dupes, ct ),
		"spawn_prop_pile" => SpawnPile( ct ),
		"shoot" => Shoot( ct ),
		"tool_constraint" => ToolConstraint( ct ),
		"tool_other" => ToolOther( ct ),
		"duplicate" => Duplicate( ct ),
		"physgun" => PhysgunDrag( ct ),
		"gravgun" => GravPull( ct ),
		"use" => Use( ct ),
		"undo" => Undo( ct ),
		"suicide" => Suicide( ct ),
		"flashlight" => Tap( "Flashlight", ct ),
		_ => Wander( ct ),
	};

	//
	// Helpers
	//

	static Task Frame() => GameTask.Yield();

	static Task Wait( float seconds, CancellationToken ct ) => GameTask.DelaySeconds( seconds, ct );

	async Task Tap( string action, CancellationToken ct )
	{
		_input.Tap( action );
		await Wait( 0.2f, ct );
	}

	/// <summary>
	/// Do something a player does through the menus, as this player. A bot's player runs it in its
	/// own update, where the bot is the caller of the RPCs it makes.
	/// </summary>
	void Act( Action action )
	{
		if ( !IsBot ) action();
		else if ( _player.IsValid() ) _player.GetOrAddComponent<BotActions>().Pending.Enqueue( action );
	}

	PlayerInventory Inventory => _player.IsValid() ? _player.GetComponent<PlayerInventory>() : null;

	void LookAt( Vector3 pos ) { _lookAtObject = null; _lookAt = pos; }
	void LookAt( GameObject go, Vector3 offset = default ) { _lookAtObject = go; _lookAtOffset = offset; }

	async Task<bool> AimAt( float tolerance, float timeout, CancellationToken ct )
	{
		RealTimeSince t = 0;
		while ( t < timeout )
		{
			ct.ThrowIfCancellationRequested();
			if ( !_player.IsValid() ) return false;

			Vector3? target = _lookAtObject.IsValid() ? _lookAtObject.WorldPosition + _lookAtOffset : _lookAt;
			if ( target is null ) return false;

			var eye = _player.EyeTransform;
			var dir = (target.Value - eye.Position).Normal;
			if ( Vector3.GetAngle( eye.Forward, dir ) < tolerance ) return true;

			await Frame();
		}

		return false;
	}

	Vector3 Origin => _player.IsValid() ? _player.WorldPosition : Vector3.Zero;

	Vector3 RandomGroundNear( float min, float max )
	{
		if ( !_player.IsValid() ) return Vector3.Zero;
		var dir = Rotation.FromYaw( Random.Shared.Float( 0, 360 ) ).Forward;
		var p = Origin + dir * Random.Shared.Float( min, max ) + Vector3.Up * 200;
		var tr = _player.Scene.Trace.Ray( p, p + Vector3.Down * 2000 ).WithoutTags( "player" ).Run();
		return tr.Hit ? tr.EndPosition : p.WithZ( Origin.z );
	}

	/// <summary>
	/// A random physics object nearby, preferring things we can see.
	/// </summary>
	GameObject RandomProp( float radius = 1500, bool includeWorld = false )
	{
		if ( !_player.IsValid() ) return null;
		var scene = _player.Scene;
		var candidates = scene.FindInPhysics( new Sphere( Origin, radius ) )
			.Where( x => x.Tags.Has( "removable" ) || x.GetComponent<Rigidbody>().IsValid() )
			.Where( x => !x.Tags.Has( "player" ) && x.Root != _player.GameObject )
			.Select( x => x.Root )
			.Distinct()
			.Take( 64 )
			.ToList();

		if ( candidates.Count == 0 ) return null;
		return Random.Shared.FromList( candidates );
	}

	GameObject RandomTarget()
	{
		if ( !_player.IsValid() ) return null;
		var scene = _player.Scene;
		var roll = Random.Shared.Float();

		if ( roll < 0.55f )
		{
			var players = scene.GetAll<Player>().Where( x => x != _player && x.Health > 0 ).ToList();
			if ( players.Count > 0 ) return Random.Shared.FromList( players ).GameObject;
		}

		if ( roll < 0.8f )
		{
			var npcs = scene.GetAll<Npc>().ToList();
			if ( npcs.Count > 0 ) return Random.Shared.FromList( npcs ).GameObject;
		}

		return RandomProp();
	}

	void Spawn( string ident )
	{
		if ( string.IsNullOrEmpty( ident ) ) return;

		Act( () => GameManager.Spawn( ident ) );
	}

	async Task EnsureWeapon<T>( string prefab, CancellationToken ct ) where T : BaseSandboxWeapon
	{
		var inv = Inventory;
		if ( inv is null ) return;

		if ( !inv.HasWeapon<T>() )
		{
			Act( () => inv.Pickup( prefab, false ) );
			await Wait( 0.3f, ct );
		}

		BaseSandboxWeapon w = inv.GetWeapon<T>();
		if ( w.IsValid() && inv.ActiveWeapon != w )
		{
			Act( () => inv.SwitchWeapon( w ) );
			await Wait( 0.4f, ct );
		}
	}

	async Task SetToolMode( string mode, CancellationToken ct )
	{
		var inv = Inventory;
		if ( inv is null ) return;

		Act( () => inv.SetToolMode( mode ) );
		await Wait( 0.5f, ct );
	}

	//
	// Actions
	//

	async Task Wander( CancellationToken ct )
	{
		_moveTo = RandomGroundNear( 200, 1200 );
		if ( _wantNoclip ) _moveTo += Vector3.Up * Random.Shared.Float( 50, 500 );
		_sprint = Random.Shared.Float() < 0.4f;
		LookAt( _moveTo.Value + Vector3.Up * 64 );

		RealTimeSince t = 0;
		var duration = Random.Shared.Float( 2, 6 );
		while ( t < duration && _moveTo is not null )
		{
			if ( Random.Shared.Float() < 0.01f ) _input.Tap( "jump" );
			if ( Random.Shared.Float() < 0.005f ) _input.SetAction( "duck", !_input.IsDown( "duck" ) );
			await Frame();
			ct.ThrowIfCancellationRequested();
		}

		_moveTo = null;
		_input.SetAction( "duck", false );
	}

	async Task ToggleNoclip( CancellationToken ct )
	{
		_wantNoclip = !_wantNoclip;
		await Wander( ct );
	}

	async Task SpawnSomething( List<string> list, CancellationToken ct )
	{
		var ident = BotContent.Pick( list );
		if ( ident is null )
		{
			await Wander( ct );
			return;
		}

		// Look at the ground a little ahead, like a player would
		LookAt( RandomGroundNear( 120, 400 ) );
		await AimAt( 6, 1.5f, ct );

		Spawn( ident );
		await Wait( Random.Shared.Float( 0.5f, 1.5f ), ct );
	}

	async Task SpawnPile( CancellationToken ct )
	{
		var count = Random.Shared.Int( 3, 12 );
		var ident = BotContent.Pick( Random.Shared.Float() < 0.5f && BotContent.Featured.Count > 0 ? BotContent.Featured : BotContent.Props );
		LookAt( RandomGroundNear( 150, 400 ) );
		await AimAt( 6, 1.5f, ct );

		for ( int i = 0; i < count; i++ )
		{
			// Same thing over and over, or a mix
			Spawn( Random.Shared.Float() < 0.6f ? ident : BotContent.Pick( BotContent.Props ) );
			await Wait( Random.Shared.Float( 0.1f, 0.4f ), ct );
		}
	}

	async Task Shoot( CancellationToken ct )
	{
		var inv = Inventory;
		if ( inv is null ) return;

		var guns = inv.Weapons.Where( x => x is not global::Physgun && x is not Toolgun && x is not CameraWeapon ).ToList();
		if ( guns.Count == 0 || Random.Shared.Float() < 0.15f )
		{
			// Get a gun the way players do - spawn it from the menu
			var weapon = BotContent.Pick( BotContent.Weapons );
			LookAt( RandomGroundNear( 100, 200 ) );
			Spawn( weapon );
			await Wait( 1.0f, ct );
			guns = inv.Weapons.Where( x => x is not global::Physgun && x is not Toolgun && x is not CameraWeapon ).ToList();
			if ( guns.Count == 0 ) return;
		}

		inv = Inventory;
		if ( !inv.IsValid() ) return;

		var gun = Random.Shared.FromList( guns );
		if ( inv.ActiveWeapon != gun )
		{
			Act( () => inv.SwitchWeapon( gun ) );
			await Wait( 0.5f, ct );
		}

		var target = RandomTarget();
		if ( !target.IsValid() ) return;

		LookAt( target, Vector3.Up * 40 );
		_moveTo = target.WorldPosition;
		_sprint = false;

		await AimAt( 8, 2, ct );

		RealTimeSince t = 0;
		var duration = Random.Shared.Float( 1.5f, 5f );
		while ( t < duration && target.IsValid() )
		{
			// Bursts, with some secondary fire and reloads
			_input.SetAction( "attack1", Random.Shared.Float() < 0.85f );
			if ( Random.Shared.Float() < 0.01f ) _input.Tap( "attack2" );
			if ( Random.Shared.Float() < 0.005f ) _input.Tap( "reload" );
			if ( (target.WorldPosition - Origin).Length < 250 ) _moveTo = null;

			await Frame();
			ct.ThrowIfCancellationRequested();
		}

		_input.SetAction( "attack1", false );
		_moveTo = null;
		if ( Random.Shared.Float() < 0.5f ) _input.Tap( "reload" );
	}

	async Task ToolConstraint( CancellationToken ct )
	{
		var mode = BotContent.Pick( BotContent.ConstraintTools );
		if ( mode is null ) return;

		await SetToolMode( mode, ct );

		var a = RandomProp( 800 );
		if ( !a.IsValid() )
		{
			await SpawnSomething( BotContent.Props, ct );
			return;
		}

		// First point on a prop, second on another prop or the world
		LookAt( a );
		_moveTo = (a.WorldPosition - Origin).Length > 500 ? a.WorldPosition : null;
		if ( !await AimAt( 3, 3, ct ) ) return;
		_moveTo = null;
		await Tap( "attack1", ct );

		var b = Random.Shared.Float() < 0.7f ? RandomProp( 800 ) : null;
		if ( b.IsValid() && b != a ) LookAt( b );
		else LookAt( RandomGroundNear( 100, 400 ) );

		await AimAt( 3, 3, ct );
		await Tap( "attack1", ct );

		// Sometimes remove all constraints again
		if ( Random.Shared.Float() < 0.1f )
		{
			LookAt( a );
			await AimAt( 3, 2, ct );
			await Tap( "reload", ct );
		}
	}

	async Task ToolOther( CancellationToken ct )
	{
		var mode = BotContent.Pick( BotContent.OtherTools );
		if ( mode is null ) return;

		await SetToolMode( mode, ct );

		var clicks = Random.Shared.Int( 1, 4 );
		for ( int i = 0; i < clicks; i++ )
		{
			var target = Random.Shared.Float() < 0.75f ? RandomProp( 800 ) : null;
			if ( target.IsValid() ) LookAt( target );
			else LookAt( RandomGroundNear( 100, 400 ) );

			await AimAt( 4, 2, ct );

			var roll = Random.Shared.Float();
			await Tap( roll < 0.7f ? "attack1" : roll < 0.9f ? "attack2" : "reload", ct );
			await Wait( Random.Shared.Float( 0.2f, 0.6f ), ct );
		}
	}

	async Task Duplicate( CancellationToken ct )
	{
		var target = RandomProp( 800 );
		if ( !target.IsValid() ) return;

		await SetToolMode( "duplicator", ct );

		LookAt( target );
		if ( !await AimAt( 3, 3, ct ) ) return;
		await Tap( "attack2", ct );

		var pastes = Random.Shared.Int( 1, 3 );
		for ( int i = 0; i < pastes; i++ )
		{
			LookAt( RandomGroundNear( 150, 500 ) );
			await AimAt( 4, 2, ct );
			await Tap( "attack1", ct );
			await Wait( 0.5f, ct );
		}
	}

	async Task PhysgunDrag( CancellationToken ct )
	{
		await EnsureWeapon<global::Physgun>( "weapons/physgun/physgun.prefab", ct );

		var target = RandomProp( 700 );
		if ( !target.IsValid() ) return;

		LookAt( target );
		if ( !await AimAt( 2, 3, ct ) ) return;

		_input.SetAction( "attack1", true );
		await Wait( 0.2f, ct );

		// Drag it around
		RealTimeSince t = 0;
		var duration = Random.Shared.Float( 1, 4 );
		_turnSpeed = 120;
		while ( t < duration )
		{
			LookAt( Origin + Rotation.FromYaw( Random.Shared.Float( 0, 360 ) ).Forward * 300 + Vector3.Up * Random.Shared.Float( 0, 300 ) );
			if ( Random.Shared.Float() < 0.05f ) _input.AddMouseWheel( new Vector2( 0, Random.Shared.Float( -2, 2 ) ) );
			await Wait( Random.Shared.Float( 0.3f, 0.8f ), ct );
		}
		_turnSpeed = 360;

		// Freeze it, or let go
		if ( Random.Shared.Float() < 0.5f )
			await Tap( "attack2", ct );

		_input.SetAction( "attack1", false );

		if ( Random.Shared.Float() < 0.1f )
			await Tap( "reload", ct );
	}

	async Task GravPull( CancellationToken ct )
	{
		await EnsureWeapon<global::Physgun>( "weapons/physgun/physgun.prefab", ct );

		var target = RandomProp( 600 );
		if ( !target.IsValid() ) return;

		LookAt( target );
		if ( !await AimAt( 3, 3, ct ) ) return;

		_input.SetAction( "attack2", true );
		await Wait( Random.Shared.Float( 0.5f, 1.5f ), ct );

		// Launch it at someone
		var victim = RandomTarget();
		if ( victim.IsValid() ) LookAt( victim );
		await Wait( 0.4f, ct );
		await Tap( "attack1", ct );
		_input.SetAction( "attack2", false );
	}

	async Task Use( CancellationToken ct )
	{
		var target = RandomProp( 400 );
		if ( !target.IsValid() ) return;

		LookAt( target );
		_moveTo = target.WorldPosition;
		await AimAt( 4, 3, ct );
		await Wait( 0.5f, ct );
		_moveTo = null;
		await Tap( "use", ct );

		// Sat in something? get out again later
		await Wait( Random.Shared.Float( 1, 4 ), ct );
		await Tap( "use", ct );
	}

	async Task Undo( CancellationToken ct )
	{
		var times = Random.Shared.Int( 1, 3 );
		for ( int i = 0; i < times; i++ )
		{
			if ( IsBot )
				UndoSystem.Current?.For( (long)Connection.SteamId.ValueUnsigned ).Undo();
			else
				_input.Tap( "undo" );

			await Wait( 0.3f, ct );
		}
	}

	async Task Suicide( CancellationToken ct )
	{
		_input.Tap( "die" );
		await Wait( 1f, ct );
	}
}
