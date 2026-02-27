using Sandbox.Rendering;

public partial class Toolgun : ScreenWeapon
{
	protected override void DrawScreenContent( Rect rect, HudPainter paint )
	{
		var currentMode = GetCurrentMode();
		currentMode?.DrawScreen( rect, paint );
	}
}
