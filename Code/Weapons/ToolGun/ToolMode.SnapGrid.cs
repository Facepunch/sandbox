public abstract partial class ToolMode
{
	/// <summary>
	/// When true (default), a SnapGrid overlay is shown on the hovered object surface.
	/// Override to return false to opt out.
	/// </summary>
	public virtual bool UseSnapGrid => false;

	/// <summary>
	/// When true, aiming at a skinned model with a skeleton shows its bones instead of the grid,
	/// and snapping goes to the joint nearest the aim. Needs <see cref="UseSnapGrid"/>.
	/// </summary>
	public virtual bool UseBoneSnap => false;

	/// <summary>
	/// Label for the snap hint, shown whenever this tool uses the snap grid.
	/// </summary>
	public virtual string SnapAction => UseSnapGrid && !AbsorbMouseInput ? (SnapRenderer.IsValid() ? "#tool.hint.snap_bone" : "#tool.hint.snap") : null;

	/// <summary>
	/// The active snap grid, updated every frame in <see cref="OnControl"/>.
	/// Null when snap grid is disabled or the tool is inactive.
	/// </summary>
	protected SnapGrid SnapGrid { get; private set; }

	/// <summary>
	/// The skinned model whose bones are snapped to instead of the grid, or null.
	/// </summary>
	protected SkinnedModelRenderer SnapRenderer { get; private set; }

	/// <summary>
	/// The joint on <see cref="SnapRenderer"/> nearest the aim.
	/// </summary>
	protected BoneCollection.Bone SnapBone { get; private set; }

	/// <summary>
	/// Where snapping puts the aim this frame, the grid's snap point or <see cref="SnapBone"/>'s joint.
	/// </summary>
	protected Vector3 SnapWorldPosition { get; private set; }

	/// <summary>
	/// While snapping, the unsnapped aim driven by the mouse. The grid is traced from this,
	/// and the real camera is steered onto the resulting snap point.
	/// </summary>
	private Angles? _snapFreeAim;

	/// <summary>
	/// True when the snap grid or a bone was updated from a valid target this frame.
	/// </summary>
	private bool _hasSnapTarget;

	private Transform[] _snapBoneWorld;

	private void DisableSnapGrid()
	{
		SnapGrid?.Destroy();
		SnapGrid = null;
		SnapRenderer = null;
		SnapBone = null;
		_snapFreeAim = null;
		_hasSnapTarget = false;
	}

	/// <summary>
	/// While hovering an object, hold alt key to keep the camera aim snapped to the grid or bone
	/// </summary>
	public virtual void OnCameraMove( Player player, ref Angles angles )
	{
		// Skip when the mode is absorbing mouse input for its own purposes (e.g. Weld rotation stage).
		if ( AbsorbMouseInput || (SnapGrid == null && SnapRenderer == null) || !Input.Down( "snap" ) )
		{
			_snapFreeAim = null;
			return;
		}

		var currentAngles = player.Controller.EyeAngles;

		var freeAim = (_snapFreeAim ?? currentAngles) + angles;
		freeAim.pitch = freeAim.pitch.Clamp( -89f, 89f );
		_snapFreeAim = freeAim;

		var desiredAngles = _hasSnapTarget
			? Rotation.LookAt( SnapWorldPosition - player.EyeTransform.Position ).Angles()
			: freeAim;

		angles = desiredAngles - currentAngles;
	}

	/// <summary>
	/// The ray snapping aims along, free aim while snapping, otherwise the eyes.
	/// </summary>
	private Ray SnapAimRay( Player player )
	{
		return _snapFreeAim is { } freeAim
			? new Ray( player.EyeTransform.Position, freeAim.Forward )
			: player.EyeTransform.ForwardRay;
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

		var player = Toolgun?.Owner;
		var preview = player.IsValid() ? TraceFromRay( SnapAimRay( player ), 4096, player.GameObject ) : default;
		var hasTarget = preview.IsValid() && ShouldDisplaySnapGrid( preview.GameObject );

		// Skinned models snap to their bones, everything else to the grid
		if ( hasTarget && UseBoneSnap && BoneEditSystem.FindRenderer( preview.GameObject.FindNetworkRoot() ?? preview.GameObject.Root ) is { } smr )
		{
			SnapGrid?.Hide();
			UpdateBoneSnap( smr, SnapAimRay( player ) );
			return;
		}

		SnapRenderer = null;
		SnapBone = null;
		_hasSnapTarget = hasTarget;

		if ( _hasSnapTarget )
		{
			SnapGrid ??= new SnapGrid();
			SnapGrid.Update( Scene.SceneWorld, preview.GameObject, preview.WorldPosition(), preview.WorldTransform().Rotation.Forward );
			SnapWorldPosition = SnapGrid.LastSnapWorldPos;
		}
		else
		{
			SnapGrid?.Hide();
		}
	}

	/// <summary>
	/// Show the skeleton and pick the joint nearest the aim, highlighted while snapping to it.
	/// </summary>
	private void UpdateBoneSnap( SkinnedModelRenderer smr, Ray aim )
	{
		SnapRenderer = smr;
		SkeletonOverlay.ReadBones( smr, ref _snapBoneWorld );

		var bones = smr.Model.Bones.AllBones;
		BoneCollection.Bone best = null;
		var bestAngle = float.MaxValue;

		for ( int i = 0; i < bones.Count; i++ )
		{
			var angle = Vector3.GetAngle( aim.Forward, _snapBoneWorld[i].Position - aim.Position );
			if ( angle >= bestAngle ) continue;

			bestAngle = angle;
			best = bones[i];
		}

		SnapBone = best;
		_hasSnapTarget = best is not null;

		if ( best is not null )
			SnapWorldPosition = _snapBoneWorld[best.Index].Position;

		if ( Scene.Camera is { IsValid: true } camera )
		{
			var snapping = Input.Down( "snap" );
			SkeletonOverlay.Draw( camera, smr, _snapBoneWorld, selected: snapping ? best : null, hovered: best, labelled: best );
		}
	}
}
