using Sandbox;
using System.Linq;

partial class SandboxGame
{
	SceneObject dragSceneObject;

	Vector3 GetBoundsOffset( BBox bounds, Vector3 dir )
	{
		var point = bounds.Center + -dir * bounds.Volume;

		return dir * Vector3.Zero.Distance( bounds.ClosestPoint( point ) );
	}

	/// <summary>
	/// Something has been dragged and dropped on the game view. This is usually a file
	/// from the asset browser. This is only called by the server host - so it's usually
	/// safe to do stuff here without checking for cheats, depending on your game.
	/// </summary>
	/// <param name="text">The text that has been dragged. Possibly the path of a file.</param>
	/// <param name="ray">The ray from the view for you to trace and place an object in the world</param>
	/// <param name="action">One of "drop", "leave" or "hover"</param>
	public override bool OnDragDropped( string text, Ray ray, string action )
	{
		if ( action == "leave" )
		{
			dragSceneObject?.Delete();
			dragSceneObject = null;
			return true;
		}

		var tr = Trace.Ray( ray, 2000.0f )
			.WithAnyTags( "world", "static", "solid" )
			.WithoutTags( "player", "npc" )
			.Run();

		var pos = tr.HitPosition;
		var rot = Rotation.From( new Angles( 0, Rotation.LookAt( ray.Forward, tr.Normal ).Angles().yaw, 0 ) ) * Rotation.FromAxis( Vector3.Up, 180 );

		// If multiple things, get the first one..
		text = text.Split( new char[] { '\n', '\r' } ).FirstOrDefault();

		// If we're a compiled asset path, trim it
		if ( text.EndsWith( "_c" ) )
			text = text[..^2];

		//
		// Spawn a model
		//
		if ( text.EndsWith( ".vmdl" ) )
		{
			if ( action == "hover" )
			{
				dragSceneObject ??= new SceneObject( Game.SceneWorld, text );
				dragSceneObject.Position = pos + GetBoundsOffset( dragSceneObject.LocalBounds, tr.Normal );
				dragSceneObject.Rotation = rot;
			}

			if ( action == "drop" )
			{
				var modelEnt = new Prop();
				modelEnt.SetModel( text );
				modelEnt.Position = pos + GetBoundsOffset( dragSceneObject.LocalBounds, tr.Normal );
				modelEnt.Rotation = rot;

				modelEnt.SetupPhysicsFromModel( PhysicsMotionType.Dynamic, false );
			}

			return true;
		}

		//
		// Spawn a prefab
		//
		if ( text.EndsWith( ".prefab" ) )
		{
			if ( action == "hover" )
			{
				// todo - we should be able to draw a one frame preview using the same
				// drawing logic that the prefab editor etc will use.

				// failing that can we store the bbox in the prefab and use that?
			}

			if ( action == "drop" )
			{
				var modelEnt = PrefabLibrary.Spawn<Entity>( text );
				if ( modelEnt != null )
				{
					modelEnt.Position = pos;
					modelEnt.Rotation = rot;
				}

				//modelEnt.SetupPhysicsFromModel( PhysicsMotionType.Dynamic, true );
			}

			return true;
		}

		//
		// Cloud model or something
		//
		if ( text.StartsWith( "https://asset.party/" ) )
		{
			if ( !Package.TryGetCached( text, out var package, false ) )
			{
				_ = Package.FetchAsync( text, false );
				return true;
			}

			if ( package.PackageType == Package.Type.Model )
			{
				var model = package.GetMeta( "PrimaryAsset", "models/dev/error.vmdl" );
				var mins = package.GetMeta( "RenderMins", Vector3.Zero );
				var maxs = package.GetMeta( "RenderMaxs", Vector3.Zero );

				if ( action == "hover" )
				{
					if ( package.IsMounted( true ) )
					{
						dragSceneObject ??= new SceneObject( Game.SceneWorld, model );
						dragSceneObject.Position = pos + GetBoundsOffset( dragSceneObject.LocalBounds, tr.Normal );
						dragSceneObject.Rotation = rot;
						dragSceneObject.ColorTint = Color.White.WithAlpha( 0.6f );
					}
					else
					{
						// preview
						DebugOverlay.Box( pos, rot, mins, maxs, Color.White, 0.01f, true );
					}
				}

				if ( action == "drop" )
				{
					//if ( package.IsMounted( true ) )
					{
						var modelEnt = new Prop();
						modelEnt.SetModel( model );
						modelEnt.Position = pos + GetBoundsOffset( dragSceneObject.LocalBounds, tr.Normal );
						modelEnt.Rotation = rot;

						modelEnt.SetupPhysicsFromModel( PhysicsMotionType.Dynamic, false );
					}
					//else
					{
						// todo - drop a preview entity which will turn into the model when created
					}
				}

				return true;
			}

			// unhandled package type
		}


		return false;
	}
}
