using Sandbox.Rendering;

public partial class Toolgun : BaseCarryable
{
	public override void OnCameraMove( Player player, ref Angles angles )
	{
		base.OnCameraMove( player, ref angles );
	}

	protected override void OnAwake()
	{
		if ( IsProxy )
			return;
	}

	public void CreateToolComponents()
	{
		if ( !Networking.IsHost )
		{
			Log.Warning( "CreateToolComponents should be called on the host" );
			return;
		}

		bool enabled = true;

		// create every available mode, but disabled
		foreach ( var mode in Game.TypeLibrary.GetTypes<ToolMode>() )
		{
			if ( mode.IsAbstract ) continue;

			Components.Create( mode, enabled );
			enabled = false;
		}
	}

	float _coilSpin = 0;
	public override void OnControl( Player player )
	{
		var currentMode = GetCurrentMode();
		if ( currentMode == null )
			return;

		currentMode.OnControl();

		UpdateViewmodelScreen();

		base.OnControl( player );

		if ( currentMode.TraceSelect().IsValid() )
		{
			if ( Input.Pressed( "Attack1" ) )
			{
				_coilSpin += 10;
			}
		}

		_coilSpin = _coilSpin.LerpTo( 0, Time.Delta * 1 );

		if ( !ViewModel.IsValid() ) return;
		var coil = ViewModel.GetAllObjects( true ).FirstOrDefault( x => x.Name == "coil" );
		coil.WorldRotation *= Rotation.From( 0, 0, _coilSpin );
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		var currentMode = GetCurrentMode();
		currentMode?.DrawHud( painter, crosshair );
	}

	public ToolMode GetCurrentMode() => GetComponent<ToolMode>();

	public T GetMode<T>() where T : ToolMode
	{
		return GetComponent<T>( true );
	}

	[Rpc.Host]
	public void SetToolMode( string name )
	{
		var targetMode = Game.TypeLibrary.GetType<ToolMode>( name );
		if ( targetMode == null )
		{
			Log.Warning( $"Unknown Mode {name}" );
			return;
		}

		var newMode = GetComponents<ToolMode>( true ).Where( x => x.GetType() == targetMode.TargetType ).FirstOrDefault();
		if ( newMode == null )
		{
			Log.Warning( $"Toolgun missing mode component for {name}" );
			return;
		}

		var currentMode = GetCurrentMode();

		// already in this mode
		if ( newMode == currentMode )
			return;

		if ( currentMode != null )
		{
			currentMode.Enabled = false;
		}

		newMode.Enabled = true;
		GameObject.Enabled = true;
		Network.Refresh( GameObject );
	}
}
