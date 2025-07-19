
[Icon( "🍄" )]
[ClassName( "resizer" )]
public class Resizer : ToolMode
{
	public override void OnControl()
	{
		var go = TraceSelect().GameObject;
		if ( !go.IsValid() ) return;

		if ( Input.Pressed( "attack1" ) ) Resize( go, 0.1f );
		else if ( Input.Pressed( "attack2" ) ) Resize( go, -0.1f );
	}

	[Rpc.Host]
	public void Resize( GameObject go, float size )
	{
		if ( !go.IsValid() ) return;

		var scale = Vector3.Max( go.WorldScale + size, 0.01f );
		go.WorldScale = scale;
	}
}
