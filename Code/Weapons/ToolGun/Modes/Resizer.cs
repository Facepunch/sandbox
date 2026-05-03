﻿
[Icon( "🍄" )]
[Title( "#tool.name.resizer" )]
[ClassName( "resizer" )]
[Group( "#tool.group.tools" )]
public class Resizer : ToolMode
{
	public override IEnumerable<string> TraceIgnoreTags => [];

	public override string Description => "#tool.hint.resizer.description";

	TimeSince timeSinceAction = 0;

	protected override void OnStart()
	{
		base.OnStart();

		RegisterAction( ToolInput.Primary, () => "#tool.hint.resizer.grow", OnGrow, InputMode.Down );
		RegisterAction( ToolInput.Secondary, () => "#tool.hint.resizer.shrink", OnShrink, InputMode.Down );
		RegisterAction( ToolInput.Reload, () => "#tool.hint.resizer.reset", OnReset );
	}

	void OnGrow()
	{
		var select = TraceSelect();
		IsValidState = select.IsValid() && !select.IsWorld;
		if ( !IsValidState ) return;
		if ( timeSinceAction < 0.03f ) return;

		Resize( select.GameObject, 0.033f );
		timeSinceAction = 0;
	}

	void OnShrink()
	{
		var select = TraceSelect();
		IsValidState = select.IsValid() && !select.IsWorld;
		if ( !IsValidState ) return;
		if ( timeSinceAction < 0.03f ) return;

		Resize( select.GameObject, -0.033f );
		timeSinceAction = 0;
	}

	void OnReset()
	{
		var select = TraceSelect();
		IsValidState = select.IsValid() && !select.IsWorld;
		if ( !IsValidState ) return;

		ResetScale( select.GameObject );
		ShootEffects( select );
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
