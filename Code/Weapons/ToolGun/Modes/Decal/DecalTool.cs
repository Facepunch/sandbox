
using Sandbox.UI;

[Title( "#tool.name.decal" )]
[Icon( "🖌️" )]
[ClassName( "decaltool" )]
[Group( "#tool.group.render" )]
public sealed class DecalTool : ToolMode
{
	[Property, ResourceSelect( Extension = "decal", AllowPackages = true ), Title( "#tool.name.decal" )]
	public string Decal { get; set; }

	[Property, Title( "#tool.setting.tint" )]
	public Color Tint { get; set; } = Color.White;

	[Range( 0.1f, 4f ), Step( 0.1f ), Separator]
	[Property, Title( "#tool.setting.scale" )]
	public float Scale { get; set; } = 1f;

	/// <summary>
	/// Multiplies the decal's own attenuation angle, lower fades less at grazing angles.
	/// </summary>
	[Range( 0f, 1f ), Step( 0.05f )]
	[Property, Title( "#tool.setting.attenuation" )]
	public float Attenuation { get; set; } = 1f;

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
			HidePreview();
			return;
		}

		if ( !IsProxy ) UpdatePreview();

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
		DestroyPreview();
	}

	protected override void OnDestroy()
	{
		StopPaintSound();
		DestroyPreview();
		base.OnDestroy();
	}

	// Decal preview visuals settings
	const float PreviewOpacityMax = 0.25f;
	const float PreviewOpacityMin = 0.1f;
	const float PreviewPulsePeriod = 1f; // how long it takes to go from Max to Min, and vice versa

	/// <summary>
	/// Local client preview of the selected decal where the player is looking
	/// </summary>
	GameObject _preview;
	Decal _previewDecal;

	void UpdatePreview()
	{
		var select = TraceSelect();
		var def = string.IsNullOrEmpty( Decal ) ? null : ResourceLibrary.Get<DecalDefinition>( Decal );

		if ( !select.IsValid() || def == null )
		{
			HidePreview();
			return;
		}

		// recreated if something removed it, like a map cleanup
		if ( !_preview.IsValid() || !_previewDecal.IsValid() )
		{
			_preview?.Destroy();
			_preview = new GameObject( false, "decal preview" );
			_preview.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked | GameObjectFlags.Hidden;
			_preview.NetworkMode = NetworkMode.Never;

			_previewDecal = _preview.AddComponent<Decal>();
		}

		var pos = select.WorldTransform();
		_preview.WorldPosition = pos.Position + pos.Rotation.Forward * 1f;
		_preview.WorldRotation = Rotation.LookAt( -pos.Rotation.Forward );

		if ( _previewDecal.Decals is not [var current] || current != def )
			_previewDecal.Decals = [def];

		var pulse = 0.5f + 0.5f * MathF.Cos( Time.Now * MathF.Tau / PreviewPulsePeriod );
		var opacity = MathX.Lerp( PreviewOpacityMin, PreviewOpacityMax, pulse );
		_previewDecal.ColorTint = Tint.WithAlpha( Tint.a * opacity );
		_previewDecal.Scale = Scale.Clamp( 0.1f, 4f );
		_previewDecal.AttenuationAngle = Attenuation.Clamp( 0f, 1f );

		_preview.Enabled = true;
	}

	void HidePreview()
	{
		if ( _preview.IsValid() ) _preview.Enabled = false;
	}

	void DestroyPreview()
	{
		if ( _preview.IsValid() ) _preview.Destroy();
		_preview = null;
		_previewDecal = null;
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

		SpawnDecal( select, resource, Tint, Scale, Attenuation, 0 );
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
		SpawnDecal( select, resource, Tint, Scale, Attenuation, _sprayId );
	}

	uint _layer = 0;

	/// <summary>
	/// Spawn a decal on the host. A non-zero <paramref name="sprayId"/> groups the decal with
	/// every other decal sent with the same id into a single undo entry.
	/// The settings come with the call, since only the owner has their current values.
	/// </summary>
	[Rpc.Host]
	public void SpawnDecal( SelectionPoint point, DecalDefinition def, Color tint, float scale, float attenuation, int sprayId )
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
		decal.ColorTint = tint;
		decal.Scale = scale.Clamp( 0.1f, 4f );
		decal.AttenuationAngle *= attenuation.Clamp( 0f, 1f );

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
