/// <summary>
/// Put this in a prefab to frame its thumbnail - it's rendered from this object's position and
/// rotation instead of auto-framing the whole prefab. Point it at what should be in shot.
/// </summary>
[Title( "Thumbnail Camera" ), Icon( "photo_camera" )]
public sealed class ThumbnailCamera : Component
{
	/// <summary>
	/// Keep it narrow and pull the camera back - a long lens flattens the perspective and looks
	/// more cinematic than a wide one up close.
	/// </summary>
	[Property, Range( 5, 90 )]
	public float FieldOfView { get; set; } = 15f;

	/// <summary>
	/// How far ahead to draw the framing in the editor - just a guide, it doesn't affect the render.
	/// </summary>
	[Property]
	public float GizmoDistance { get; set; } = 120f;

	protected override void DrawGizmos()
	{
		var half = GizmoDistance * MathF.Tan( FieldOfView * 0.5f * MathF.PI / 180f );
		var corners = new[]
		{
			new Vector3( GizmoDistance, half, half ),
			new Vector3( GizmoDistance, -half, half ),
			new Vector3( GizmoDistance, -half, -half ),
			new Vector3( GizmoDistance, half, -half ),
		};

		Gizmo.Draw.Color = Color.Cyan;

		for ( var i = 0; i < corners.Length; i++ )
		{
			Gizmo.Draw.Line( Vector3.Zero, corners[i] );
			Gizmo.Draw.Line( corners[i], corners[(i + 1) % corners.Length] );
		}
	}

	/// <summary>
	/// Render <paramref name="prefab"/> to <paramref name="bitmap"/> through the thumbnail camera it
	/// contains. Returns false if it doesn't have one.
	/// </summary>
	public static bool TryRender( PrefabFile prefab, Bitmap bitmap )
	{
		var source = prefab?.GetScene();
		var framing = source?.GetComponentInChildren<ThumbnailCamera>( true );
		if ( !framing.IsValid() )
			return false;

		// Where the camera sits relative to the prefab root
		var view = source.WorldTransform.ToLocal( framing.WorldTransform );
		var fieldOfView = framing.FieldOfView;

		// An editor scene, so nothing in the prefab thinks it's live gameplay (random loadouts etc.)
		var scene = Scene.CreateEditorScene();

		using ( scene.Push() )
		{
			var root = GameObject.Clone( prefab, new CloneConfig { Transform = global::Transform.Zero, StartEnabled = true } );

			var sun = new GameObject( true, "sun" ).Components.Create<DirectionalLight>();
			sun.WorldRotation = Rotation.From( 45, 200, 0 );
			sun.LightColor = Color.White * 2.5f;
			sun.SkyColor = Color.White * 0.6f;

			var camera = new GameObject( true, "camera" ).Components.Create<CameraComponent>();
			camera.WorldTransform = root.WorldTransform.ToWorld( view );
			camera.FieldOfView = fieldOfView;
			camera.BackgroundColor = Color.Transparent;

			camera.RenderToBitmap( bitmap );
		}

		scene.Destroy();
		return true;
	}
}
