public abstract partial class ToolMode
{
	/// <summary>
	/// When true (default), a SnapGrid overlay is shown on the hovered object surface.
	/// Override to return false to opt out.
	/// </summary>
	public virtual bool UseSnapGrid => false;

	/// <summary>
	/// Label for the snap hint, shown whenever this tool uses the snap grid.
	/// </summary>
	public virtual string SnapAction => UseSnapGrid && !AbsorbMouseInput ? "#tool.hint.snap" : null;

	/// <summary>
	/// The active snap grid, updated every frame in <see cref="OnControl"/>.
	/// Null when snap grid is disabled or the tool is inactive.
	/// </summary>
	protected SnapGrid SnapGrid { get; private set; }

	/// <summary>
	/// While snapping, the unsnapped aim driven by the mouse. The grid is traced from this,
	/// and the real camera is steered onto the resulting snap point.
	/// </summary>
	private Angles? _snapFreeAim;

	/// <summary>
	/// True when the snap grid was updated from a valid surface this frame.
	/// </summary>
	private bool _hasSnapTarget;

	private void DisableSnapGrid()
	{
		SnapGrid?.Destroy();
		SnapGrid = null;
		_snapFreeAim = null;
		_hasSnapTarget = false;
	}

	/// <summary>
	/// While hovering an object, hold alt key to keep the camera aim snapped to the grid
	/// </summary>
	public virtual void OnCameraMove( Player player, ref Angles angles )
	{
		// Skip when the mode is absorbing mouse input for its own purposes (e.g. Weld rotation stage).
		if ( AbsorbMouseInput || SnapGrid == null || !Input.Down( "snap" ) )
		{
			_snapFreeAim = null;
			return;
		}

		var currentAngles = player.Controller.EyeAngles;

		var freeAim = (_snapFreeAim ?? currentAngles) + angles;
		freeAim.pitch = freeAim.pitch.Clamp( -89f, 89f );
		_snapFreeAim = freeAim;

		var desiredAngles = _hasSnapTarget
			? Rotation.LookAt( SnapGrid.LastSnapWorldPos - player.EyeTransform.Position ).Angles()
			: freeAim;

		angles = desiredAngles - currentAngles;
	}

	/// <summary>
	/// The ray the snap grid is traced from, free aim while snapping, otherwise the eyes.
	/// </summary>
	private SelectionPoint TraceSnapGrid()
	{
		var player = Toolgun?.Owner;
		if ( !player.IsValid() ) return default;

		if ( _snapFreeAim is not { } freeAim )
			return TraceSelect();

		var ray = new Ray( player.EyeTransform.Position, freeAim.Forward );
		return TraceFromRay( ray, 4096, player.GameObject );
	}

	/// <summary>
	/// Override to control which objects show the snap grid. Excludes player geometry by default.
	/// </summary>
	protected virtual bool ShouldDisplaySnapGrid( GameObject go )
	{
		return !go.Tags.Has( "player" );
	}

	public virtual void OnControl()
	{
		DispatchActions();

		if ( !UseSnapGrid ) return;

		var preview = TraceSnapGrid();
		_hasSnapTarget = preview.IsValid() && ShouldDisplaySnapGrid( preview.GameObject );
		if ( _hasSnapTarget )
		{
			SnapGrid ??= new SnapGrid();
			SnapGrid.Update( Scene.SceneWorld, preview.GameObject, preview.WorldPosition(), preview.WorldTransform().Rotation.Forward );
		}
		else
		{
			SnapGrid?.Hide();
		}
	}
}
