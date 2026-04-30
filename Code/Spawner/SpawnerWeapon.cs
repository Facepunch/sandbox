using Sandbox.Rendering;

/// <summary>
/// A weapon that previews and places objects into the world. 
/// Accepts any <see cref="ISpawner"/> to define what to spawn.
/// The spawn menu (or any other system) sets the payload, and this weapon handles
/// aiming, previewing, and placement.
/// </summary>
public partial class SpawnerWeapon : ScreenWeapon, IToolInfo
{
	/// <summary>
	/// Synced payload descriptor. When this changes on any client,
	/// <see cref="OnPayloadDataChanged"/> reconstructs the <see cref="ISpawner"/> locally.
	/// </summary>
	[Sync( SyncFlags.FromHost ), Change( nameof( OnPayloadDataChanged ) )]
	public string SpawnerData { get; set; }

	/// <summary>
	/// The local spawner, built from <see cref="SpawnerData"/>.
	/// </summary>
	public ISpawner Spawner { get; private set; }

	/// <summary>
	/// Override the inventory icon with the payload's cloud thumbnail.
	/// </summary>
	public override string InventoryIconOverride => Spawner?.Icon switch
	{
		null => null,
		var icon when icon.StartsWith( "http", StringComparison.OrdinalIgnoreCase ) => icon,
		var icon => $"thumb:{icon}"
	};

	/// <summary>
	/// Whether the current aim position is a valid placement target.
	/// </summary>
	private bool _isValidPlacement;

	private Material _previewMaterial;
	private Material _previewMaterialInvalid;

	/// <summary>
	/// True while the player is holding Use to rotate the preview.
	/// </summary>
	private bool _isRotating;

	/// <summary>
	/// Accumulated rotation offset applied to the spawn preview.
	/// </summary>
	private Rotation _rotationOffset = Rotation.Identity;

	private Rotation _snapRotation = Rotation.Identity;
	private Rotation _spinRotation = Rotation.Identity;
	private bool _isSnapping;

	public override void OnCameraMove( Player player, ref Angles angles )
	{
		base.OnCameraMove( player, ref angles );

		if ( _isRotating )
		{
			angles = default;
		}
	}

	protected override void OnStart()
	{
		base.OnStart();

		_previewMaterial = Material.Load( "materials/effects/duplicator_override.vmat" );
		_previewMaterialInvalid = Material.Load( "materials/effects/duplicator_override_other.vmat" );
	}

	/// <summary>
	/// Set what this spawner should spawn. Serializes the payload and syncs to all clients via <see cref="SpawnerData"/>.
	/// </summary>
	public void SetSpawner( ISpawner payload )
	{
		Spawner = payload;
		SyncPayload( SerializeSpawner( payload ) );
	}

	/// <summary>
	/// Clear the current payload, returning to an idle state.
	/// </summary>
	public void ClearPayload()
	{
		SetSpawner( null );
	}

	/// <summary>
	/// Directly restores a previously serialized payload string
	/// </summary>
	public void RestoreSpawnerData( string serialisedData )
	{
		SyncPayload( serialisedData );
	}

	[Rpc.Host]
	private void SyncPayload( string data )
	{
		SpawnerData = data;
	}

	/// <summary>
	/// Called on every client when <see cref="SpawnerData"/> changes.
	/// Reconstructs the <see cref="ISpawner"/> locally so each client can render the preview.
	/// </summary>
	private void OnPayloadDataChanged()
	{
		Spawner = DeserializeSpawner( SpawnerData );
	}
	/// <summary>
	/// Serialize a spawner for networking to <c>type:data</c>
	/// </summary>
	private static string SerializeSpawner( ISpawner spawner ) => spawner switch
	{
		PropSpawner => $"prop:{spawner.Data}",
		EntitySpawner => $"entity:{spawner.Data}",
		DuplicatorSpawner => $"dupe:{spawner.Data}",
		_ => null
	};

	/// <summary>
	/// Reconstruct an <see cref="ISpawner"/> from <c>type:data</c>
	/// </summary>
	private static ISpawner DeserializeSpawner( string data )
	{
		if ( string.IsNullOrWhiteSpace( data ) )
			return null;

		var colonIndex = data.IndexOf( ':' );
		if ( colonIndex < 0 )
			return null;

		var type = data[..colonIndex];
		var value = data[(colonIndex + 1)..];

		return type switch
		{
			"prop" => new PropSpawner( value ),
			"entity" => new EntitySpawner( value ),
			"dupe" => DuplicatorSpawner.FromData( value ),
			_ => null
		};
	}

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		UpdateViewmodelScreen();
		ApplyCoilSpin();


		if ( Spawner is null )
			return;

		_isRotating = Input.Down( "use" );
		SetIsUsingJoystick( _isRotating );

		var isSnapping = Input.Down( "run" );
		if ( !isSnapping && _isSnapping ) _spinRotation = _snapRotation;
		_isSnapping = isSnapping;

		if ( _isRotating )
		{
			var look = Input.AnalogLook with { pitch = 0 } * 1;

			if ( _isSnapping )
			{
				if ( MathF.Abs( look.yaw ) > MathF.Abs( look.pitch ) ) look.pitch = 0;
				else look.yaw = 0;
			}

			_spinRotation = Rotation.From( look ) * _spinRotation;
			Input.Clear( "use" );

			if ( _isSnapping )
			{
				var snapped = _spinRotation.Angles();
				_rotationOffset = snapped.SnapToGrid( 45f );
			}
			else
			{
				_rotationOffset = _spinRotation;
			}

			_snapRotation = _rotationOffset;

			UpdateJoystick( new Angles( look.yaw, look.pitch, 0 ) );
		}


		var placement = GetPlacementInfo( player );
		_isValidPlacement = placement.Hit;

		if ( _isValidPlacement && Spawner.IsReady && Input.Pressed( "attack1" ) )
		{
			var transform = GetSpawnTransform( placement, player );
			DoSpawn( transform );
			_rotationOffset = Rotation.Identity;
		}

		if ( Input.Pressed( "attack2" ) )
		{
			RemoveFromInventory();
		}
	}

	/// <summary>
	/// Remove this weapon from the player's inventory entirely.
	/// Holsters first, then destroys the game object.
	/// </summary>
	[Rpc.Host]
	private void RemoveFromInventory()
	{
		var inventory = Owner?.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() ) return;

		inventory.SwitchWeapon( null );
		DestroyGameObject();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( Spawner is null ) return;
		if ( !Owner.IsValid() ) return;

		// Draw preview on all clients, so everyone can see what's being placed
		DrawPreview();
	}

	private void DrawPreview()
	{
		var player = Owner;

		var placement = GetPlacementInfo( player );
		if ( !placement.Hit ) return;

		var transform = GetSpawnTransform( placement, player );

		// Use a different material for other players' previews, same as the Duplicator
		var material = IsProxy
			? _previewMaterialInvalid
			: (_isValidPlacement && Spawner.IsReady) ? _previewMaterial : _previewMaterialInvalid;

		Spawner.DrawPreview( transform, material );
	}

	private SceneTraceResult GetPlacementInfo( Player player )
	{
		return Scene.Trace.Ray( player.EyeTransform.ForwardRay, 4096 )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.WithoutTags( "player" )
			.Run();
	}

	private Transform GetSpawnTransform( SceneTraceResult trace, Player player )
	{
		var up = trace.Normal;
		var backward = -player.EyeTransform.Forward;
		var right = Vector3.Cross( up, backward ).Normal;
		var forward = Vector3.Cross( right, up ).Normal;
		var facingAngle = Rotation.LookAt( forward, up );

		var position = trace.EndPosition;

		// Offset by bounds so the object sits on the surface
		if ( Spawner is not null )
		{
			position += up * -Spawner.Bounds.Mins.z;
		}

		return new Transform( position, facingAngle * _rotationOffset );
	}

	[Rpc.Host]
	private async void DoSpawn( Transform transform )
	{
		if ( Spawner is null ) return;

		var player = Player.FindForConnection( Rpc.Caller );
		if ( player is null ) return;

		var spawnData = new Global.ISpawnEvents.SpawnData
		{
			Spawner = Spawner,
			Transform = transform,
			Player = player?.PlayerData
		};

		Scene.RunEvent<Global.ISpawnEvents>( x => x.OnSpawn( spawnData ) );

		if ( spawnData.Cancelled )
			return;

		var objects = await Spawner.Spawn( transform, player );

		if ( objects is { Count: > 0 } )
		{
			var undo = player.Undo.Create();
			undo.Name = $"Spawn {Spawner.DisplayName}";

			foreach ( var go in objects )
			{
				undo.Add( go );
			}

			Scene.RunEvent<Global.ISpawnEvents>( x => x.OnPostSpawn( new Global.ISpawnEvents.PostSpawnData
			{
				Spawner = Spawner,
				Transform = transform,
				Player = player?.PlayerData,
				Objects = objects
			} ) );
		}
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		if ( Spawner is null )
		{
			// Idle crosshair
			painter.SetBlendMode( BlendMode.Normal );
			painter.DrawCircle( crosshair, 3, Color.White.WithAlpha( 0.3f ) );
			return;
		}

		var color = (_isValidPlacement && Spawner.IsReady) ? Color.White : new Color( 0.9f, 0.3f, 0.2f );

		painter.SetBlendMode( BlendMode.Normal );
		painter.DrawCircle( crosshair, 5, color.Darken( 0.3f ) );
		painter.DrawCircle( crosshair, 3, color );
	}

	protected override void DrawScreenContent( Rect rect, HudPainter paint )
	{
		var icon = Texture.Load( this.InventoryIconOverride );
		if ( icon is not null )
		{
			var size = rect.Height;
			var iconRect = new Rect(
				rect.Center.x - size * 0.5f,
				rect.Center.y - size * 0.5f,
				size,
				size
			);
			paint.DrawTexture( icon, iconRect );
		}
	}


	string IToolInfo.Name => "Spawner";
	string IToolInfo.Description => $"Placing {Spawner.DisplayName}";
	string IToolInfo.PrimaryAction => "Spawn";
	string IToolInfo.SecondaryAction => "Clear";
}
