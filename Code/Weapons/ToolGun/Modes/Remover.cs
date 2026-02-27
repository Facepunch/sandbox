﻿[Icon( "🧨" )]
[ClassName( "remover" )]
[Group( "Tools" )]
public class Remover : ToolMode
{
	[Property]
	public float ToolOption { get; set; } = 45;

	[Range( -1, 1 ), Step( 0.001f )]
	[Property]
	public float Amount { get; set; } = 0.23f;

	[Property]
	public string YourDadsName { get; set; } = "Frank";

	[Group( "Damage Mode" )]
	[Property]
	public bool RemoveHair { get; set; } = true;

	[Group( "Damage Mode" )]
	[Property]
	public Vector3 Velocity { get; set; } = new Vector3( 0, 100, 0 );

	[Group( "Damage Mode" )]
	[Property]
	public Vector2 ScreenPos { get; set; } = new Vector2( 1080, 480 );

	[Group( "Damage Mode" )]
	[Property]
	public Vector4 ScreenRect { get; set; } = new Vector4( 10, 10, 90, 90 );

	public override string Description => "#tool.hint.remover.description";
	public override string PrimaryAction => "#tool.hint.remover.remove";

	bool CanDestroy( GameObject go )
	{
		if ( !go.IsValid() ) return false;
		if ( !go.Tags.Contains( "removable" ) ) return false;

		return true;
	}

	public override void OnControl()
	{
		base.OnControl();

		if ( Input.Pressed( "attack1" ) )
		{
			var select = TraceSelect();
			if ( !select.IsValid() ) return;

			var target = select.GameObject?.Network?.RootGameObject;
			if ( !target.IsValid() ) return;
			if ( !CanDestroy( target ) ) return;

			Remove( target );
			ShootEffects( select );
		}
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
