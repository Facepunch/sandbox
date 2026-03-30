﻿
[Icon( "🍄" )]
[ClassName( "resizer" )]
[Group( "Tools" )]
public class Resizer : ToolMode
{
	public override IEnumerable<string> TraceIgnoreTags => [];

	public override string Description => "#tool.hint.resizer.description";
	public override string PrimaryAction => "#tool.hint.resizer.grow";
	public override string SecondaryAction => "#tool.hint.resizer.shrink";
	public override string ReloadAction => "#tool.hint.resizer.reset";

	TimeSince timeSinceAction = 0;

	public override void OnControl()
	{
		var select = TraceSelect();

		IsValidState = select.IsValid() && !select.IsWorld;
		if ( !IsValidState )
			return;

		if ( timeSinceAction < 0.03f )
			return;

		if ( Input.Down( "attack1" ) )
		{
			Resize( select.GameObject, 0.033f );
			timeSinceAction = 0;
		}
		else if ( Input.Down( "attack2" ) )
		{
			Resize( select.GameObject, -0.033f );
			timeSinceAction = 0;
		}
		else if ( Input.Pressed( "reload" ) )
		{
			ResetScale( select.GameObject );
			ShootEffects( select );
		}
	}

	[Rpc.Broadcast]
	private void ResetScale( GameObject go )
	{
		if ( !go.IsValid() ) return;
		if ( go.IsProxy ) return;

		go.WorldScale = Vector3.One;
	}

	[Rpc.Broadcast]
	private void Resize( GameObject go, float size )
	{
		if ( !go.IsValid() ) return;
		if ( go.IsProxy ) return;

		var newScale = go.WorldScale + size;
		if ( newScale.Length < 0.1f ) return;
		if ( newScale.Length > 1000f ) return;

		var scale = Vector3.Max( newScale, 0.01f );
		go.WorldScale = scale;
	}
}
