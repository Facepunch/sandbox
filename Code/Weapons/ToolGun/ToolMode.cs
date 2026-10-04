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

	/// <summary>
	/// Reflection info for this tool's type. Lazy so it's valid before the component starts.
	/// </summary>
	public TypeDescription TypeDescription => _typeDescription ??= TypeLibrary.GetType( GetType() );
	private TypeDescription _typeDescription;

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
			Player = Player?.Network.Owner
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
			Player = Player?.Network.Owner,
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

	/// <summary>
	/// Draws the tool title, scrolling long titles within the viewmodel screen.
	/// </summary>
	public virtual void DrawScreen( Rect rect, Painter paint )
	{
		using var state = paint.Scope();
		paint.Clip( rect );
		paint.TextStyle = new TextStyle( "Poppins", 64, Color.Orange )
		{
			LineHeight = 0.75f,
			FontWeight = 700,
			Alignment = TextFlag.Center | TextFlag.SingleLine
		};

		var title = Game.Language.GetPhrase( TypeDescription.Title.TrimStart( '#' ) );
		var text = $"{TypeDescription.Icon} {title}";
		var textWidth = paint.MeasureText( text ).x;

		if ( textWidth <= rect.Width )
		{
			paint.Text( text, rect );
			return;
		}

		// Marquee: clip both copies to the screen while they loop right-to-left.
		const float scrollSpeed = 80f;
		const float gap = 60f;
		var cycle = textWidth + gap;
		var offset = (Time.Now * scrollSpeed) % cycle;
		var x = rect.Right - offset;

		paint.TextStyle = paint.TextStyle with { Alignment = TextFlag.LeftCenter | TextFlag.SingleLine };
		paint.Text( text, new Rect( x, rect.Top, textWidth, rect.Height ) );
		paint.Text( text, new Rect( x - cycle, rect.Top, textWidth, rect.Height ) );
	}

	/// <summary>
	/// Draws a crosshair showing whether the current tool action is valid.
	/// </summary>
	public virtual void DrawHud( Painter painter, Vector2 crosshair )
	{
		using var state = painter.Scope();
		painter.BlendMode = BlendMode.Normal;

		Color invalidColor = "#e53";
		painter.Fill = IsValidState ? Color.White : invalidColor;
		painter.Stroke = Stroke.Solid( IsValidState ? Color.Black : invalidColor.Darken( 0.3f ), 1f )
			.WithAlignment( Stroke.StrokeAlignment.Outside );
		painter.Circle( crosshair, 1.5f );
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
