using Sandbox.Rendering;

public partial class Toolgun : BaseCarryable
{
	[Sync]
	public string Mode { get; set; } = "easyweld";

	ToolMode CurrentMode;

	public override void OnCameraMove( Player player, ref Angles angles )
	{
		base.OnCameraMove( player, ref angles );
	}

	public override void OnControl( Player player )
	{
		if ( CurrentMode == null )
		{
			CurrentMode = CreateToolMode( Mode );
		}

		if ( CurrentMode == null )
			return;

		CurrentMode.OnControl();

		UpdateViewmodelScreen();

		base.OnControl( player );
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		CurrentMode?.DrawHud( painter, crosshair );
	}

	public void SwitchMode( string mode )
	{
		CurrentMode = default;
		Mode = mode;
	}

	ToolMode CreateToolMode( string name )
	{
		var mode = Game.TypeLibrary.GetType<ToolMode>( name );
		if ( mode is null )
		{
			Log.Warning( $"Couldn't create tool mode {name}" );
			return default;
		}

		var created = mode.Create<ToolMode>( null );
		created.InitializeInternal( this );
		return created;
	}
}
