using Sandbox.Rendering;

public partial class Toolgun : BaseCarryable
{
	public override void OnCameraMove( Player player, ref Angles angles )
	{
		base.OnCameraMove( player, ref angles );
	}

	protected override void OnStart()
	{
		if ( IsProxy )
			return;

		SetToolMode( "remover" );
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
		foreach ( var c in GetComponents<ToolMode>( true ) )
		{
			c.Destroy();
		}

		var td = Game.TypeLibrary.GetType<ToolMode>( name );
		if ( td != null )
		{
			var newMode = Components.Create( td, true );

			// newMode on enabled
		}

		GameObject.Enabled = true;
		Network.Refresh();
	}
}
