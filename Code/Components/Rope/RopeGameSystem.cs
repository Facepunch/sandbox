namespace Sandbox;

using System.Diagnostics;
/// <summary>
/// Simulates VerletRope components in parallel during PrePhysicsStep
/// </summary>
sealed class RopeGameSystem : GameObjectSystem
{
	public RopeGameSystem( Scene scene ) : base( scene )
	{
		// Listen to StartFixedUpdate to run before physics
		Listen( Stage.StartFixedUpdate, -100, UpdateRopes, "UpdateRopes" );
	}

	void UpdateRopes()
	{
		Stopwatch sw = Stopwatch.StartNew();

		var ropes = Scene.GetAll<VerletRope>();
		if ( ropes.Count() == 0 ) return;

		var timeDelta = Time.Delta;
		Sandbox.Utility.Parallel.ForEach( ropes, rope => rope.Simulate( timeDelta ) );

		sw.Stop();

		//DebugOverlaySystem.Current.ScreenText( new Vector2( 120, 30 ), $"Rope Sim: {sw.Elapsed.TotalMilliseconds,6:F2} ms", 24 );
	}
}
