
using Sandbox.UI;

[Title( "#tool.name.decal" )]
[Icon( "🖌️" )]
[ClassName( "decaltool" )]
[Group( "#tool.group.render" )]
public sealed class DecalTool : ToolMode
{
	[Property, ResourceSelect( Extension = "decal", AllowPackages = true ), Title( "#tool.name.decal" )]
	public string Decal { get; set; }

	public override string Description => "#tool.hint.decaltool.description";

	TimeSince timeSinceShoot = 0;
	[Sync] bool IsPainting { get; set; }
	SoundHandle _paintSound;

	protected override void OnStart()
	{
		base.OnStart();

		RegisterAction( ToolInput.Primary, () => "#tool.hint.decaltool.place", OnPlace );
		RegisterAction( ToolInput.Secondary, () => "#tool.hint.decaltool.paint", OnPaint, InputMode.Down );
	}

	public override void OnControl()
	{
		IsPainting = Input.Down( "attack2" );
		base.OnControl();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		var toolgun = Toolgun;
		var player = toolgun?.Owner;
		var active = toolgun.IsValid() && player.IsValid()
			&& player.GetComponent<PlayerInventory>()?.ActiveWeapon == toolgun;

		if ( !active )
		{
			if ( !IsProxy ) IsPainting = false;
			StopPaintSound();
			return;
		}

		if ( IsPainting )
		{
			var position = toolgun.GetMuzzleTransform().Position;
			_paintSound ??= Sound.Play( "weapons/toolgun/sounds/shoot.spray.sound", position );
			_paintSound?.Position = position;
		}
		else
		{
			StopPaintSound();
		}
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		if ( !IsProxy ) IsPainting = false;
		StopPaintSound();
	}

	protected override void OnDestroy()
	{
		StopPaintSound();
		base.OnDestroy();
	}

	void StopPaintSound()
	{
		_paintSound?.Stop( 0.2f );
		_paintSound = null;
	}

	[Rpc.Broadcast]
	void PlayPlaceSound()
	{
		if ( !Toolgun.IsValid() ) return;
		Sound.Play( "weapons/toolgun/sounds/shoot.decal.sound", Toolgun.GetMuzzleTransform().Position );
	}

	void OnPlace()
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var resource = ResourceLibrary.Get<DecalDefinition>( Decal );
		if ( resource == null ) return;

		SpawnDecal( select, resource );
		PlayPlaceSound();
	}

	void OnPaint()
	{
		if ( timeSinceShoot < 0.05f ) return;

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var resource = ResourceLibrary.Get<DecalDefinition>( Decal );
		if ( resource == null ) return;

		timeSinceShoot = 0;
		SpawnDecal( select, resource );
	}

	uint _layer = 0;

	[Rpc.Host]
	public void SpawnDecal( SelectionPoint point, DecalDefinition def )
	{
		if ( def == null ) return;

		var pos = point.WorldTransform();

		var go = new GameObject( true, "decal" );
		go.Tags.Add( "removable" );
		go.WorldPosition = pos.Position + pos.Rotation.Forward * 1f;
		go.WorldRotation = Rotation.LookAt( -pos.Rotation.Forward );
		go.SetParent( point.GameObject, true );

		var decal = go.AddComponent<Decal>();
		decal.Decals = [def];
		decal.SortLayer = _layer++;

		go.NetworkSpawn();

		var undo = Player.Undo.Create();
		undo.Name = "#tool.name.decal";
		undo.Add( go );
	}
}
