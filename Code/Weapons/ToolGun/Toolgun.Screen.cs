public partial class Toolgun : ScreenWeapon
{
	/// <summary>
	/// Draws the current tool mode on the viewmodel screen.
	/// </summary>
	protected override void DrawScreenContent( Rect rect, Painter paint )
	{
		var currentMode = GetCurrentMode();
		currentMode?.DrawScreen( rect, paint );
	}
}
