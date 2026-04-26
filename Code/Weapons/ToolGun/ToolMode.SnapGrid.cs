public abstract partial class ToolMode
{
	/// <summary>
	/// When true (default), a SnapGrid overlay is shown on the hovered object surface.
	/// Override to return false to opt out.
	/// </summary>
	public virtual bool UseSnapGrid => false;

	/// <summary>
	/// The active snap grid, updated every frame in <see cref="OnControl"/>.
	/// Null when snap grid is disabled or the tool is inactive.
	/// </summary>
	protected SnapGrid SnapGrid { get; private set; }

	private Vector3? _lockedSnapTarget;

	private void DisableSnapGrid()
	{
		SnapGrid?.Destroy();
		SnapGrid = null;
		_lockedSnapTarget = null;
	}

	/// <summary>
	/// While hovering an object, hold E to lock the camera aim onto the nearest snap corner.
	/// </summary>
	public virtual void OnCameraMove( Player player, ref Angles angles )
	{
		// Skip when the mode is absorbing mouse input for its own purposes (e.g. Weld rotation stage).
		if ( AbsorbMouseInput || SnapGrid == null )
			return;

		if ( Input.Pressed( "use" ) )
			_lockedSnapTarget = SnapGrid.LastSnapWorldPos;

		if ( !Input.Down( "use" ) || _lockedSnapTarget == null )
			return;

		var eyePos = player.EyeTransform.Position;
		var desiredAngles = Rotation.LookAt( _lockedSnapTarget.Value - eyePos ).Angles();
		var currentAngles = player.Controller.EyeAngles;

		angles = desiredAngles - currentAngles;

		if ( Input.Released( "use" ) )
			_lockedSnapTarget = null;

		Input.Clear( "use" );
	}

	/// <summary>
	/// Override to control which objects show the snap grid. Returns false for world and player geometry by default.
	/// </summary>
	protected virtual bool ShouldDisplaySnapGrid( GameObject go )
	{
		return !go.Tags.Has( "world" ) && !go.Tags.Has( "player" );
	}

	public virtual void OnControl()
	{
		DispatchActions();

		if ( !UseSnapGrid ) return;

		var preview = TraceSelect();
		if ( preview.IsValid() && ShouldDisplaySnapGrid( preview.GameObject ) )
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
