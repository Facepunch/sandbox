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
	public virtual string Name => Game.Language.GetPhrase( (TypeDescription?.Title ?? GetType().Name).TrimStart( '#' ) );

	/// <summary>
	/// Description of what this tool does.
	/// </summary>
	public virtual string Description => string.Empty;

	/// <summary>
	/// Label for the primary action (attack1), or null if none.
	/// Auto-populated from registered <see cref="ToolActionEntry"/> when not overridden.
	/// </summary>
	public virtual string PrimaryAction => GetActionName( ToolInput.Primary );

	/// <summary>
	/// Label for the secondary action (attack2), or null if none.
	/// Auto-populated from registered <see cref="ToolActionEntry"/> when not overridden.
	/// </summary>
	public virtual string SecondaryAction => GetActionName( ToolInput.Secondary );

	/// <summary>
	/// Label for the reload action, or null if none.
	/// Auto-populated from registered <see cref="ToolActionEntry"/> when not overridden.
	/// </summary>
	public virtual string ReloadAction => GetActionName( ToolInput.Reload );

	/// <summary>
	/// Tags that TraceSelect will ignore. Override per-tool to filter out specific objects.
	/// Defaults to "player" so tools cannot target players.
	/// </summary>
	public virtual IEnumerable<string> TraceIgnoreTags => ["player"];

	/// <summary>
	/// When true, TraceSelect will also hit hitboxes.
	/// </summary>
	public virtual bool TraceHitboxes => false;

	public TypeDescription TypeDescription { get; protected set; }

	private readonly List<ToolActionEntry> _actions = new();
	private readonly List<GameObject> _createdObjects = new();

	/// <summary>
	/// Register a tool action that will be dispatched automatically by the base <see cref="OnControl"/>.
	/// The display name is a lambda so it can vary with tool state (e.g. stage-dependent hints).
	/// </summary>
	protected void RegisterAction( ToolInput input, Func<string> name, Action callback, InputMode mode = InputMode.Pressed )
	{
		if ( IsProxy ) return;

		_actions.Add( new ToolActionEntry( input, name, callback, mode ) );
	}

	/// <summary>
	/// Track a GameObject created by this tool action. These are passed through
	/// to <see cref="IToolActionEvents.PostActionData.CreatedObjects"/> when the post-event fires.
	/// </summary>
	protected void Track( params GameObject[] objects )
	{
		foreach ( var go in objects )
		{
			if ( go.IsValid() )
				_createdObjects.Add( go );
		}
	}

	/// <summary>
	/// Returns the display name for the first registered action matching <paramref name="input"/>, or null.
	/// </summary>
	private string GetActionName( ToolInput input )
	{
		foreach ( var action in _actions )
		{
			if ( action.Input == input )
				return action.Name?.Invoke();
		}

		return null;
	}

	/// <summary>
	/// Fire <see cref="IToolActionEvents.OnToolAction"/> before executing an action.
	/// Returns true if the action should proceed, false if cancelled.
	/// </summary>
	protected bool FireToolAction( ToolInput input )
	{
		var data = new IToolActionEvents.ActionData
		{
			Tool = this,
			Input = input,
			Player = Player?.PlayerData
		};

		Scene.RunEvent<IToolActionEvents>( x => x.OnToolAction( data ) );
		return !data.Cancelled;
	}

	/// <summary>
	/// Fire <see cref="IToolActionEvents.OnPostToolAction"/> after a successful action.
	/// Passes a snapshot of tracked objects, then clears the list.
	/// </summary>
	protected void FirePostToolAction( ToolInput input )
	{
		var objects = _createdObjects.Count > 0 ? new List<GameObject>( _createdObjects ) : null;
		_createdObjects.Clear();

		Scene.RunEvent<IToolActionEvents>( x => x.OnPostToolAction( new IToolActionEvents.PostActionData
		{
			Tool = this,
			Input = input,
			Player = Player?.PlayerData,
			CreatedObjects = objects
		} ) );
	}

	/// <summary>
	/// Check registered actions and invoke any whose input condition is met this frame.
	/// Wraps each callback with <see cref="IToolActionEvents"/> pre/post events.
	/// </summary>
	private void DispatchActions()
	{
		foreach ( var action in _actions )
		{
			var inputName = action.InputAction;
			if ( inputName is null ) continue;

			bool active = action.Mode == InputMode.Down
				? Input.Down( inputName )
				: Input.Pressed( inputName );

			if ( active )
			{
				if ( !FireToolAction( action.Input ) )
					continue;

				_createdObjects.Clear();
				action.Callback?.Invoke();
				FirePostToolAction( action.Input );
			}
		}
	}

	protected override void OnStart()
	{
		TypeDescription = TypeLibrary.GetType( GetType() );
	}

	protected override void OnEnabled()
	{
		if ( Network.IsOwner )
		{
			this.LoadCookies();
		}
	}

	protected override void OnDisabled()
	{
		DisableSnapGrid();

		if ( Network.IsOwner )
		{
			this.SaveCookies();
		}
	}

	public virtual void DrawScreen( Rect rect, HudPainter paint )
	{
		var title = Game.Language.GetPhrase( TypeDescription.Title.TrimStart( '#' ) );
		var t = $"{TypeDescription.Icon} {title}";

		var text = new TextRendering.Scope( t, Color.White, 64 );
		text.LineHeight = 0.75f;
		text.FontName = "Poppins";
		text.TextColor = Color.Orange;
		text.FontWeight = 700;

		var measured = text.Measure();
	    float textW = measured.x;
	    float textH = measured.y;
	
	    if ( textW <= rect.Width )
	    {
	        paint.DrawText( text, rect, TextFlag.Center );
	        return;
	    }
	
	    // Marquee: scroll text right-to-left, looping seamlessly.
	    // The render target viewport naturally clips anything outside [0, rect.Width].
	    const float scrollSpeed = 80f;
	    const float gap = 60f;
	    float cycle = textW + gap;
	    float offset = (Time.Now * scrollSpeed) % cycle;
	
	    float y = rect.Top + (rect.Height - textH) * 0.5f;
	
	    float x = rect.Width - offset;
	    paint.DrawText( text, new Rect( x, y, textW, textH ), TextFlag.SingleLine | TextFlag.Left );
	    paint.DrawText( text, new Rect( x - cycle, y, textW, textH ), TextFlag.SingleLine | TextFlag.Left );
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

	/// <summary>
	/// Called on the host after placing an entity or constraint. Fires an RPC to the owning
	/// client so it can walk the contraption graph and record achievement stats locally.
	/// </summary>
	[Rpc.Owner]
	protected void CheckContraptionStats( GameObject anchor )
	{
		var builder = new LinkedGameObjectBuilder();
		builder.AddConnected( anchor );

		var wheels = builder.Objects.Sum( o => o.GetComponentsInChildren<WheelEntity>().Count() );
		var thrusters = builder.Objects.Sum( o => o.GetComponentsInChildren<ThrusterEntity>().Count() );
		var hoverballs = builder.Objects.Sum( o => o.GetComponentsInChildren<HoverballEntity>().Count() );
		var constraints = builder.Objects.Sum( o => o.GetComponentsInChildren<ConstraintCleanup>().Count() );
		var chairs = builder.Objects.Sum( o => o.GetComponentsInChildren<BaseChair>().Count() );

		Sandbox.Services.Stats.Increment( "tool.constraint.create", 1 );
		Sandbox.Services.Stats.SetValue( "tool.contraption.wheel", wheels );
		Sandbox.Services.Stats.SetValue( "tool.contraption.thruster", thrusters );
		Sandbox.Services.Stats.SetValue( "tool.contraption.hoverball", hoverballs );
		Sandbox.Services.Stats.SetValue( "tool.contraption.constraint", constraints );
		Sandbox.Services.Stats.SetValue( "tool.contraption.chair", chairs );
	}
}
