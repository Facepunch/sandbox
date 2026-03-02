using Sandbox.Rendering;

public abstract partial class ToolMode : Component, IToolInfo
{
	public Toolgun Toolgun => GetComponent<Toolgun>();
	public Player Player => GetComponentInParent<Player>();

	/// <summary>
	/// The mode should set this true or false in OnControl to indicate if the current state is valid for performing actions.
	/// </summary>
	public bool IsValidState { get; protected set; } = true;

	/// <summary>
	/// When true, the toolgun will absorb mouse input so the camera doesn't move.
	/// The mode can then read <see cref="Input.AnalogLook"/> to use the mouse for rotation etc.
	/// </summary>
	public virtual bool AbsorbMouseInput => false;

	/// <summary>
	/// Display name for the tool, defaults to the TypeDescription title.
	/// </summary>
	public virtual string Name => TypeDescription?.Title ?? GetType().Name;

	/// <summary>
	/// Description of what this tool does.
	/// </summary>
	public virtual string Description => string.Empty;

	/// <summary>
	/// Label for the primary action (attack1), or null if none.
	/// </summary>
	public virtual string PrimaryAction => null;

	/// <summary>
	/// Label for the secondary action (attack2), or null if none.
	/// </summary>
	public virtual string SecondaryAction => null;

	/// <summary>
	/// Label for the reload action, or null if none.
	/// </summary>
	public virtual string ReloadAction => null;

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
