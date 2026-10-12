
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

	/// <summary>
	/// Owner-side id of the current spray
	/// </summary>
	int _sprayId;

	/// <summary>
	/// Host-side undo entry for the spray identified by <see cref="_hostSprayId"/>.
	/// </summary>
	UndoSystem.Entry _sprayUndo;
	int _hostSprayId;

	public override void OnControl()
	{
		var wasPainting = IsPainting;
		IsPainting = Input.Down( "attack2" );

		if ( IsPainting && !wasPainting ) _sprayId++;

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

		SpawnDecal( select, resource, 0 );
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
		SpawnDecal( select, resource, _sprayId );
	}

	uint _layer = 0;

	/// <summary>
	/// Spawn a decal on the host. A non-zero <paramref name="sprayId"/> groups the decal with
	/// every other decal sent with the same id into a single undo entry.
	/// </summary>
	[Rpc.Host]
	public void SpawnDecal( SelectionPoint point, DecalDefinition def, int sprayId )
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

		GetUndoEntry( sprayId ).Add( go );
	}

	/// <summary>
	/// The spray has no explicit end, the entry is created lazily with its first decal, so it is registered no matter how painting stops. 
	/// A new spray id, or the entry leaving the history (undone mid-spray, evicted, stack cleared), starts a new entry
	/// </summary>
	UndoSystem.Entry GetUndoEntry( int sprayId )
	{
		var stack = Player.Undo;

		if ( sprayId == 0 )
		{
			var single = stack.Create();
			single.Name = "#tool.name.decal";
			return single;
		}

		if ( sprayId != _hostSprayId || !stack.Contains( _sprayUndo ) )
		{
			_hostSprayId = sprayId;
			_sprayUndo = stack.Create();
			_sprayUndo.Name = "#tool.undo.decal_spray";
		}

		return _sprayUndo;
	}
}
