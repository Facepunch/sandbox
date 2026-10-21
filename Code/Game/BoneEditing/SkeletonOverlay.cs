/// <summary>
/// Draws a renderer's skeleton, can be used by any tool
/// </summary>
public static class SkeletonOverlay
{
	static readonly Color BoneColor = new( 0.6f, 0.6f, 0.6f, 0.5f );
	static readonly Color HoveredColor = new( 0.9f, 0.9f, 0.9f );
	static readonly Color SelectedColor = new( 1.0f, 0.6f, 0.1f );
	const int GradientSteps = 8;

	/// <summary>
	/// Read every bone's world transform, reusing <paramref name="world"/> while the bone count stays the same.
	/// </summary>
	public static void ReadBones( SkinnedModelRenderer smr, ref Transform[] world )
	{
		var bones = smr.Model.Bones.AllBones;

		if ( world is null || world.Length != bones.Count )
			world = new Transform[bones.Count];

		for ( int i = 0; i < bones.Count; i++ )
		{
			if ( !smr.TryGetBoneTransform( bones[i], out world[i] ) )
				world[i] = smr.WorldTransform;
		}
	}

	/// <summary>
	/// Draw the skeleton from bones read with <see cref="ReadBones"/>, naming <paramref name="labelled"/> next to its joint.
	/// </summary>
	public static void Draw( CameraComponent camera, SkinnedModelRenderer smr, Transform[] world, BoneCollection.Bone selected = null, BoneCollection.Bone hovered = null, BoneCollection.Bone labelled = null )
	{
		var bones = smr.Model.Bones.AllBones;

		using var scope = Gizmo.Scope( "skeleton" );

		Gizmo.Draw.IgnoreDepth = true;
		Gizmo.Draw.LineThickness = 2;

		for ( int i = 0; i < bones.Count; i++ )
		{
			var bone = bones[i];
			if ( bone.Parent is not { } parent )
				continue;

			var start = world[parent.Index].Position;
			var end = world[i].Position;

			if ( parent == selected )
				DrawGradientLine( start, end );
			else if ( bone == selected )
				DrawGradientLine( end, start );
			else
			{
				Gizmo.Draw.Color = BoneColor;
				Gizmo.Draw.Line( start, end );
			}
		}

		for ( int i = 0; i < bones.Count; i++ )
		{
			var bone = bones[i];
			var position = world[i].Position;
			var isSelected = bone == selected;
			var isHovered = bone == hovered;

			Gizmo.Draw.Color = isSelected ? SelectedColor : isHovered ? HoveredColor : BoneColor;
			var radius = camera.WorldPosition.Distance( position ) * (isSelected || isHovered ? 0.006f : 0.004f);
			Gizmo.Draw.SolidSphere( position, radius, 6, 6 );
		}

		if ( labelled is not null )
		{
			Gizmo.Draw.Color = Color.White;
			Gizmo.Draw.ScreenText( labelled.Name, world[labelled.Index].Position, new Vector2( 12, -18 ), size: 14 );
		}
	}

	static void DrawGradientLine( Vector3 from, Vector3 to )
	{
		for ( int i = 0; i < GradientSteps; i++ )
		{
			var a = (float)i / GradientSteps;
			var b = (float)(i + 1) / GradientSteps;

			Gizmo.Draw.Color = Color.Lerp( SelectedColor, BoneColor, (a + b) * 0.5f );
			Gizmo.Draw.Line( Vector3.Lerp( from, to, a ), Vector3.Lerp( from, to, b ) );
		}
	}
}
