
[Icon( "🛡️" )]
[ClassName( "unbreakable" )]
[Group( "Tools" )]
public class Unbreakable : ToolMode
{
	public override string Description => "#tool.hint.unbreakable.description";
	public override string PrimaryAction => "#tool.hint.unbreakable.set";
	public override string SecondaryAction => "#tool.hint.unbreakable.unset";

	public override void OnControl()
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var prop = select.GameObject.GetComponent<Prop>();
		if ( !prop.IsValid() ) return;

		if ( Input.Pressed( "attack1" ) ) SetUnbreakable( prop, true );
		else if ( Input.Pressed( "attack2" ) ) SetUnbreakable( prop, false );
		else return;

		ShootEffects( select );
	}

	[Rpc.Host]
	private void SetUnbreakable( Prop prop, bool unbreakable )
	{
		if ( !prop.IsValid() || prop.IsProxy ) return;

		prop.Health = unbreakable ? 0 : ( prop?.Model?.Data?.Health ?? 100 );
	}
}
