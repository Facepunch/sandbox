using System.Text.Json;
using System.Text.Json.Nodes;

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

			var tx = new Transform();
			tx.Position = select.WorldPosition() + Vector3.Down * dupe.Bounds.Mins.z;

			var relative = Player.EyeTransform.Rotation.Angles();
			tx.Rotation = new Angles( 0, relative.yaw, 0 );

			Duplicate( tx );
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

			var selectionAngle = new Transform( select.WorldPosition(), Player.EyeTransform.Rotation.Angles().WithPitch( 0 ) );
			Copy( select.GameObject, selectionAngle, Input.Down( "run" ) );

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
		dupe = null;

		if ( string.IsNullOrWhiteSpace( CopiedJson ) )
			return;

		dupe = Json.Deserialize<DuplicationData>( CopiedJson );
	}

	void DrawPreview()
	{
		if ( dupe is null ) return;

		var select = TraceSelect();
		if ( !IsValidPlacementTarget( select ) ) return;

		var tx = new Transform();

		tx.Position = select.WorldPosition() + Vector3.Down * dupe.Bounds.Mins.z;

		var relative = Player.EyeTransform.Rotation.Angles();
		tx.Rotation = new Angles( 0, relative.yaw, 0 );

		var overlayMaterial = IsProxy ? Material.Load( "materials/effects/duplicator_override_other.vmat" ) : Material.Load( "materials/effects/duplicator_override.vmat" );
		foreach ( var model in dupe.PreviewModels )
		{
			DebugOverlay.Model( model.Model, transform: tx.ToWorld( model.Transform ), overlay: false, materialOveride: overlayMaterial, localBoneTransforms: model.Bones );
		}
	}


	bool IsValidTarget( SelectionPoint source )
	{
		if ( !source.IsValid() ) return false;
		if ( source.IsWorld ) return false;
		if ( source.IsPlayer ) return false;

		return true;
	}

	bool IsValidPlacementTarget( SelectionPoint source )
	{
		if ( !source.IsValid() ) return false;

		return true;
	}

	[Rpc.Host]
	public void Duplicate( Transform dest )
	{
		if ( dupe is null )
			return;

		var jsonObject = Json.ToNode( dupe ) as JsonObject;

		SceneUtility.MakeIdGuidsUnique( jsonObject );

		var undo = Player.Undo.Create();
		undo.Name = "Duplication";

		SceneUtility.RunInBatchGroup( () =>
		{
			foreach ( var entry in jsonObject["Objects"] as JsonArray )
			{
				if ( entry is JsonObject obj )
				{
					var pos = entry["Position"]?.Deserialize<Vector3>() ?? default;
					var rot = entry["Rotation"]?.Deserialize<Rotation>() ?? Rotation.Identity;

					var world = dest.ToWorld( new Transform( pos, rot ) );

					var go = new GameObject( false );
					go.Deserialize( obj, new GameObject.DeserializeOptions { TransformOverride = world } );

					go.NetworkSpawn( true, null );

					undo.Add( go );
				}
			}
		} );
	}

}
