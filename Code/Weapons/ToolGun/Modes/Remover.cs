﻿[Icon( "🧨" )]
[Title( "#tool.name.remover" )]
[ClassName( "remover" )]
[Group( "#tool.group.tools" )]
public class Remover : ToolMode
{
	public override bool TraceHitboxes => true;
	public override string Description => "#tool.hint.remover.description";

	protected override void OnStart()
	{
		base.OnStart();

		RegisterAction( ToolInput.Primary, () => "#tool.hint.remover.remove", OnRemove );
	}

	bool CanDestroy( GameObject go )
	{
		if ( !go.IsValid() ) return false;
		if ( !go.Tags.Contains( "removable" ) ) return false;

		return true;
	}

	void OnRemove()
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var target = select.GameObject?.Network?.RootGameObject;
		if ( !target.IsValid() ) return;
		if ( !CanDestroy( target ) ) return;

		Remove( target );
		ShootEffects( select );
	}

	[Rpc.Host]
	public void Remove( GameObject go )
	{
		go = go?.Network?.RootGameObject;

		if ( !CanDestroy( go ) ) return;
		if ( go.IsProxy ) return;

		go.Destroy();
	}

}
