using Sandbox.Rendering;

public abstract class ToolMode : Component
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

[Icon( "🥽" )]
[ClassName( "easyweld" )]
public class EasyWeld : ToolMode
{

}

[Icon( "🐍" )]
[ClassName( "rope" )]
public class Rope : ToolMode
{

}

[Icon( "🧨" )]
[ClassName( "remover" )]
public class Remover : ToolMode
{
	bool CanDestroy( GameObject go )
	{
		if ( !go.IsValid() ) return false;
		if ( !go.Tags.Contains( "removable" ) ) return false;

		return true;
	}

	public override void OnControl()
	{
		base.OnControl();

		if ( Input.Pressed( "attack1" ) )
		{
			var tr = Scene.Trace.Ray( Player.EyeTransform.ForwardRay, 4096 )
				.IgnoreGameObjectHierarchy( Player.GameObject )
				.Run();

			if ( !tr.Hit )
				return;

			if ( !CanDestroy( tr.GameObject ) )
			{
				// fail effect
				return;
			}


			tr.GameObject.Destroy();
		}

	}

}
