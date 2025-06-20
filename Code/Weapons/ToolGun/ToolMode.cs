using Sandbox.Rendering;

public abstract partial class ToolMode : Component
{
	public Toolgun Toolgun => GetComponent<Toolgun>();
	public Player Player => GetComponentInParent<Player>();

	public virtual void OnControl() { }

	public virtual void DrawScreen( Rect rect, HudPainter paint )
	{
		var t = $"{TypeLibrary.GetType( GetType() ).Icon} {GetType().Name}";

		var text = new TextRendering.Scope( t, Color.White, 64 );
		text.LineHeight = 0.75f;
		text.FontName = "Poppins";
		text.TextColor = Color.Orange;
		text.FontWeight = 700;

		paint.DrawText( text, rect, TextFlag.Center );

	}

	public virtual void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		painter.DrawCircle( crosshair, 5, Color.White );
	}
}
