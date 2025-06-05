public static class Extensions
{
	public static Vector3 WithAimCone( this Vector3 direction, float degrees )
	{
		var angle = Rotation.LookAt( direction );
		angle *= new Angles( Game.Random.Float( -degrees / 2.0f, degrees / 2.0f ), Game.Random.Float( -degrees / 2.0f, degrees / 2.0f ), 0 );
		return angle.Forward;
	}

	public static Vector3 WithAimCone( this Vector3 direction, float horizontalDegrees, float verticalDegrees )
	{
		var angle = Rotation.LookAt( direction );
		angle *= new Angles( Game.Random.Float( -verticalDegrees / 2.0f, verticalDegrees / 2.0f ), Game.Random.Float( -horizontalDegrees / 2.0f, horizontalDegrees / 2.0f ), 0 );
		return angle.Forward;
	}
}
