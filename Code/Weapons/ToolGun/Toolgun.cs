using Sandbox.Rendering;

public partial class Toolgun : BaseCarryable
{
	public override void OnCameraMove( Player player, ref Angles angles )
	{
		base.OnCameraMove( player, ref angles );
	}

	public override void OnControl( Player player )
	{
		var currentMode = GetCurrentMode();
		if ( currentMode == null )
			return;

		currentMode.OnControl();

		UpdateViewmodelScreen();

		base.OnControl( player );
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		var currentMode = GetCurrentMode();
		currentMode?.DrawHud( painter, crosshair );
	}

	public ToolMode GetCurrentMode() => GetComponent<ToolMode>();

	[Rpc.Host]
	public void SetToolMode( string name )
	{
		var currentMode = GetCurrentMode();
		if ( currentMode.IsValid() )
		{
			// Destroy the current mode before creating a new one
			currentMode.Destroy();
		}

		var td = Game.TypeLibrary.GetType<ToolMode>( name );
		if ( td != null )
		{
			var newMode = Components.Create( td, true );

			// newMode on enabled
		}

		Network.Refresh();
	}
}
