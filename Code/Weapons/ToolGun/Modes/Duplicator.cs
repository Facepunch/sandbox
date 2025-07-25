
[Icon( "✌️" )]
[ClassName( "duplicator" )]
[Group( "Building" )]
public class Duplicator : ToolMode
{
	SelectionPoint Point1;
	Rotation GrabRotation;

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		if ( Input.Pressed( "attack2" ) )
		{
			if ( !select.IsValid() ) return;

			Point1 = select;
			GrabRotation = Player.EyeTransform.Rotation;
		}

		if ( !Point1.IsValid() )
			return;

		var go = Point1.GameObject.Network.RootGameObject;
		var bounds = go.GetBounds();
		var wt = select.WorldTransform();
		var tx = go.WorldTransform;

		DebugOverlay.Box( bounds );

		var closestPoint = bounds.ClosestPoint( tx.Position + wt.Forward * -10000 );

		DebugOverlay.Sphere( new Sphere( closestPoint, 2 ), overlay: true );

		var offset = tx.Position - closestPoint;

		tx.Position = select.WorldPosition() + offset;

		var relative = Rotation.Difference( GrabRotation, Player.EyeTransform.Rotation ).Angles();
		tx.Rotation = new Angles( 0, relative.yaw, 0 ) * tx.Rotation;

		if ( Input.Pressed( "attack1" ) )
		{
			if ( !select.IsValid() ) return;

			Duplicate( Point1, tx );
			return;
		}

		if ( Point1.IsValid() )
		{
			DebugOverlay.GameObject( Point1.GameObject.Network.RootGameObject, transform: tx, castShadows: true, color: Color.White.WithAlpha( 0.4f ) );
		}

	}

	[Rpc.Host]
	public void Duplicate( SelectionPoint source, Transform dest )
	{
		var sourceObject = source.GameObject.Network.RootGameObject;

		CloneConfig cc = new CloneConfig
		{
			StartEnabled = false,
			Transform = dest
		};

		var copy = sourceObject.Clone( cc );
		copy.WorldScale = sourceObject.WorldScale;

		copy.NetworkSpawn( true, null );

		var undo = Player.Undo.Create();
		undo.Add( copy );
	}

}
