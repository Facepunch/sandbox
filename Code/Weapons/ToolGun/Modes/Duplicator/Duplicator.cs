using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;

[Icon( "✌️" )]
[ClassName( "duplicator" )]
[Group( "Building" )]
public class Duplicator : ToolMode
{
	/// <summary>
	/// When we right click, to "copy" something, we create a Duplication object
	/// and serialize it to Json and store it here.
	/// </summary>
	[Sync( SyncFlags.FromHost ), Change( nameof( JsonChanged ) )]
	public string CopiedJson { get; set; }

	/// <summary>
	/// This is created in JsonChanged.
	/// </summary>
	DuplicationData dupe;

	LinkedGameObjectBuilder builder = new();

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		IsValidState = IsValidTarget( select );

		if ( dupe is not null && Input.Pressed( "attack1" ) )
		{
			if ( !IsValidPlacementTarget( select ) )
			{
				// make invalid noise
				return;
			}

			var placementTransform = CalculatePlacementTransform( select );
			Duplicate( placementTransform );
			ShootEffects( select );
			return;
		}

		if ( Input.Pressed( "attack2" ) )
		{
			if ( !IsValidState )
			{
				CopiedJson = default;
				return;
			}

			var selectionTransform = CreateSelectionTransform( select );
			Copy( select.GameObject, selectionTransform, Input.Down( "run" ) );

			ShootEffects( select );
		}
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		// this is called on every client, so we can see what the other
		// players are placing. It's kind of cool.
		DrawPreview();
	}

	[Rpc.Host]
	public void Copy( GameObject obj, Transform selectionAngle, bool additive )
	{
		if ( !additive )
			builder.Clear();

		builder.AddConnected( obj );
		builder.RemoveDeletedObjects();

		var tempDupe = DuplicationData.CreateFromObjects( builder.Objects, selectionAngle );

		CopiedJson = Json.Serialize( tempDupe );
	}

	void JsonChanged()
	{
		RefreshDuplicationData();
	}

	/// <summary>
	/// Refreshes the duplication data from the JSON string
	/// </summary>
	private void RefreshDuplicationData()
	{
		dupe = null;

		if ( string.IsNullOrWhiteSpace( CopiedJson ) )
			return;

		dupe = Json.Deserialize<DuplicationData>( CopiedJson );
	}

	/// <summary>
	/// Calculates the placement transform for duplication based on selection point
	/// </summary>
	private Transform CalculatePlacementTransform( SelectionPoint select )
	{
		var tx = new Transform();
		tx.Position = select.WorldPosition() + Vector3.Down * dupe.Bounds.Mins.z;

		var relative = Player.EyeTransform.Rotation.Angles();
		tx.Rotation = new Angles( 0, relative.yaw, 0 );

		return tx;
	}

	/// <summary>
	/// Creates the selection transform for copying objects
	/// </summary>
	private Transform CreateSelectionTransform( SelectionPoint select )
	{
		return new Transform( select.WorldPosition(), Player.EyeTransform.Rotation.Angles().WithPitch( 0 ) );
	}

	void DrawPreview()
	{
		if ( dupe is null ) return;

		var select = TraceSelect();
		if ( !IsValidPlacementTarget( select ) ) return;

		var placementTransform = CalculatePlacementTransform( select );

		var overlayMaterial = IsProxy ? Material.Load( "materials/effects/duplicator_override_other.vmat" ) : Material.Load( "materials/effects/duplicator_override.vmat" );
		foreach ( var model in dupe.PreviewModels )
		{
			DebugOverlay.Model( model.Model, transform: placementTransform.ToWorld( model.Transform ), overlay: false, materialOveride: overlayMaterial );
		}
	}


	/// <summary>
	/// Validates if the selection point is a valid target for copying
	/// </summary>
	bool IsValidTarget( SelectionPoint source )
	{
		if ( !source.IsValid() ) return false;
		if ( source.IsWorld ) return false;
		if ( source.IsPlayer ) return false;

		// Additional validation: check if object is duplicatable
		var gameObject = source.GameObject?.Network?.RootGameObject ?? source.GameObject;
		if ( gameObject?.Tags.Contains( "world" ) == true ) return false;
		if ( gameObject?.IsProxy == true ) return false;

		return true;
	}

	/// <summary>
	/// Validates if the selection point is a valid target for placing duplicated objects
	/// </summary>
	bool IsValidPlacementTarget( SelectionPoint source )
	{
		if ( !source.IsValid() ) return false;

		// Could add more specific placement validation here
		// For example: check if there's enough space, valid surface, etc.
		return true;
	}

	[Rpc.Host]
	public void Duplicate( Transform dest )
	{
		if ( dupe is null )
			return;

		var jsonObject = PrepareJsonForDuplication( dupe );
		var undo = Player.Undo.Create();

		CreateObjectsFromJson( jsonObject, dest, undo );
	}

	/// <summary>
	/// Prepares the duplication JSON by making IDs unique
	/// </summary>
	private JsonObject PrepareJsonForDuplication( DuplicationData duplicationData )
	{
		var jsonObject = Json.ToNode( duplicationData ) as JsonObject;
		SceneUtility.MakeIdGuidsUnique( jsonObject );
		return jsonObject;
	}

	/// <summary>
	/// Creates GameObjects from JSON array and transforms them to the destination
	/// </summary>
	private void CreateObjectsFromJson( JsonObject jsonObject, Transform destination, object undo )
	{
		SceneUtility.RunInBatchGroup( () =>
		{
			foreach ( var entry in jsonObject["Objects"] as JsonArray )
			{
				if ( entry is JsonObject objectJson )
				{
					CreateSingleObject( objectJson, destination, undo );
				}
			}
		} );
	}

	/// <summary>
	/// Creates a single GameObject from JSON and adds it to the undo system
	/// </summary>
	private void CreateSingleObject( JsonObject objectJson, Transform destination, object undo )
	{
		var localTransform = ExtractLocalTransform( objectJson );
		var worldTransform = TransformToDestination( localTransform, destination );
		
		UpdateJsonTransform( objectJson, worldTransform );
		
		var gameObject = CreateAndSpawnObject( objectJson );
		
		// Add to undo system via reflection since undo.Add is dynamic
		var undoType = undo.GetType();
		var addMethod = undoType.GetMethod( "Add" );
		addMethod?.Invoke( undo, new object[] { gameObject } );
	}

	/// <summary>
	/// Extracts local transform from JSON object
	/// </summary>
	private Transform ExtractLocalTransform( JsonObject objectJson )
	{
		var pos = objectJson["Position"]?.Deserialize<Vector3>() ?? default;
		var rot = objectJson["Rotation"]?.Deserialize<Rotation>() ?? Rotation.Identity;
		return new Transform( pos, rot );
	}

	/// <summary>
	/// Transforms local coordinates to destination world coordinates
	/// </summary>
	private Transform TransformToDestination( Transform localTransform, Transform destination )
	{
		return destination.ToWorld( localTransform );
	}

	/// <summary>
	/// Updates the JSON object with new world transform
	/// </summary>
	private void UpdateJsonTransform( JsonObject objectJson, Transform worldTransform )
	{
		objectJson["Position"] = JsonValue.Create( worldTransform.Position );
		objectJson["Rotation"] = JsonValue.Create( worldTransform.Rotation );
	}

	/// <summary>
	/// Creates and spawns a GameObject from JSON
	/// </summary>
	private GameObject CreateAndSpawnObject( JsonObject objectJson )
	{
		var go = new GameObject( false );
		go.Deserialize( objectJson );
		go.NetworkSpawn( null );
		return go;
	}

}
