using Sandbox.Rendering;

/// <summary>
/// Used for hints in the UI, describes how to use a tool mode.
/// </summary>
public record struct ToolHint( string Description, string PrimaryAction, string SecondaryAction = null, string ReloadAction = null );

public abstract partial class ToolMode : Component
{
	public Toolgun Toolgun => GetComponent<Toolgun>();
	public Player Player => GetComponentInParent<Player>();

	/// <summary>
	/// The mode should set this true or false in OnControl to indicate if the current state is valid for performing actions.
	/// </summary>
	public bool IsValidState { get; protected set; } = true;

	/// <summary>
	/// Current tool info that we'll show on the HUD
	/// </summary>
	public virtual ToolHint Hint { get; }

	public TypeDescription TypeDescription { get; protected set; }

	protected override void OnStart()
	{
		TypeDescription = TypeLibrary.GetType( GetType() );
	}

	public virtual void OnControl() { }

	public virtual void DrawScreen( Rect rect, HudPainter paint )
	{
		var t = $"{TypeDescription.Icon} {TypeDescription.Title}";

		var text = new TextRendering.Scope( t, Color.White, 64 );
		text.LineHeight = 0.75f;
		text.FontName = "Poppins";
		text.TextColor = Color.Orange;
		text.FontWeight = 700;

		paint.DrawText( text, rect, TextFlag.Center );

	}

	public virtual void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		if ( IsValidState )
		{
			painter.SetBlendMode( BlendMode.Normal );
			painter.DrawCircle( crosshair, 5, Color.Black );
			painter.DrawCircle( crosshair, 3, Color.White );
		}
		else
		{
			Color redColor = "#e53";
			painter.SetBlendMode( BlendMode.Normal );
			painter.DrawCircle( crosshair, 5, redColor.Darken( 0.3f ) );
			painter.DrawCircle( crosshair, 3, redColor );
		}
	}
}
