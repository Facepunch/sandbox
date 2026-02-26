using Sandbox.Rendering;

/// <summary>
/// A weapon that previews and places objects into the world. 
/// Accepts any <see cref="ISpawner"/> to define what to spawn.
/// The spawn menu (or any other system) sets the payload, and this weapon handles
/// aiming, previewing, and placement.
/// </summary>
public partial class SpawnerWeapon : BaseCarryable
{
	/// <summary>
	/// Synced payload descriptor. When this changes on any client,
	/// <see cref="OnPayloadDataChanged"/> reconstructs the <see cref="ISpawner"/> locally.
	/// </summary>
	[Sync( SyncFlags.FromHost ), Change( nameof( OnPayloadDataChanged ) )]
	public string PayloadData { get; set; }

	/// <summary>
	/// The locally reconstructed payload, built from <see cref="PayloadData"/>.
	/// </summary>
	public ISpawner Payload { get; private set; }

	/// <summary>
	/// Override the inventory icon with the payload's cloud thumbnail.
	/// </summary>
	public override string InventoryIconOverride => Payload?.Icon is not null ? $"thumb:{Payload.Icon}" : null;

	/// <summary>
	/// Whether the current aim position is a valid placement target.
	/// </summary>
	private bool _isValidPlacement;

	private Material _previewMaterial;
	private Material _previewMaterialInvalid;

	protected override void OnStart()
	{
		base.OnStart();

		_previewMaterial = Material.Load( "materials/effects/duplicator_override.vmat" );
		_previewMaterialInvalid = Material.Load( "materials/effects/duplicator_override_other.vmat" );

		// Default test payload
		if ( Payload is null )
		{
			SetPayload( new PropSpawner( "facepunch.post_box" ) );
		}
	}

	/// <summary>
	/// Set what this spawner should spawn. Serializes the payload and syncs to all clients via <see cref="PayloadData"/>.
	/// </summary>
	public void SetPayload( ISpawner payload )
	{
		Payload = payload;
		SyncPayload( SerializeSpawner( payload ) );
	}

	/// <summary>
	/// Clear the current payload, returning to an idle state.
	/// </summary>
	public void ClearPayload()
	{
		SetPayload( null );
	}

	[Rpc.Host]
	private void SyncPayload( string data )
	{
		PayloadData = data;
	}

	/// <summary>
	/// Called on every client when <see cref="PayloadData"/> changes.
	/// Reconstructs the <see cref="ISpawner"/> locally so each client can render the preview.
	/// </summary>
	private void OnPayloadDataChanged()
	{
		Payload = DeserializeSpawner( PayloadData );
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
			"dupe" => DuplicatorSpawner.FromJson( value ),
			_ => null
		};
	}

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		if ( Payload is null )
			return;

		var placement = GetPlacementInfo( player );
		_isValidPlacement = placement.Hit;

		if ( _isValidPlacement && Payload.IsReady && Input.Pressed( "attack1" ) )
		{
			var transform = GetSpawnTransform( placement, player );
			DoSpawn( transform );
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

		if ( Payload is null ) return;
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
			: (_isValidPlacement && Payload.IsReady) ? _previewMaterial : _previewMaterialInvalid;

		Payload.DrawPreview( transform, material );
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
		if ( Payload is not null )
		{
			position += up * -Payload.Bounds.Mins.z;
		}

		return new Transform( position, facingAngle );
	}

	[Rpc.Host]
	private async void DoSpawn( Transform transform )
	{
		if ( Payload is null ) return;

		var player = Player.FindForConnection( Rpc.Caller );
		if ( player is null ) return;

		var objects = await Payload.Spawn( transform, player );

		if ( objects is { Count: > 0 } )
		{
			var undo = player.Undo.Create();
			undo.Name = $"Spawn {Payload.DisplayName}";

			foreach ( var go in objects )
			{
				undo.Add( go );
			}
		}
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		if ( Payload is null )
		{
			// Idle crosshair
			painter.SetBlendMode( BlendMode.Normal );
			painter.DrawCircle( crosshair, 3, Color.White.WithAlpha( 0.3f ) );
			return;
		}

		var color = (_isValidPlacement && Payload.IsReady) ? Color.White : new Color( 0.9f, 0.3f, 0.2f );

		painter.SetBlendMode( BlendMode.Normal );
		painter.DrawCircle( crosshair, 5, color.Darken( 0.3f ) );
		painter.DrawCircle( crosshair, 3, color );

		// Draw payload name near crosshair
		if ( !string.IsNullOrEmpty( Payload.DisplayName ) )
		{
			var text = new TextRendering.Scope( Payload.DisplayName, Color.White, 18 );
			text.FontName = "Poppins";
			text.FontWeight = 600;

			var textRect = new Rect( crosshair.x - 100, crosshair.y + 16, 200, 30 );
			painter.DrawText( text, textRect, TextFlag.Center );
		}
	}
}
