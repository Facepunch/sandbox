using Sandbox.UI;
using System.Text.Json;
using System.Text.Json.Nodes;

[Icon( "✌️" )]
[ClassName( "duplicator" )]
[Group( "Building" )]
public partial class Duplicator : ToolMode
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

	/// <summary>
	/// Have all packaged finished loading.
	/// </summary>
	bool packagesReady = false;

	LinkedGameObjectBuilder builder = new();

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		IsValidState = IsValidTarget( select );

		if ( dupe is not null && packagesReady && Input.Pressed( "attack1" ) )
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

	/// <summary>
	/// Save the current dupe to storage.
	/// </summary>
	public void Save()
	{
		string data = CopiedJson;
		var packages = Cloud.ResolvePrimaryAssetsFromJson( data );

		var storage = Storage.CreateEntry( "dupe" );
		storage.SetMeta( "packages", packages.Select( x => x.FullIdent ) );
		storage.Files.WriteAllText( "/dupe.json", data );

		var bitmap = new Bitmap( 1024, 1024 );
		RenderIconToBitmap( data, bitmap );

		var downscaled = bitmap.Resize( 512, 512 );
		storage.SetThumbnail( downscaled );
	}

	[Rpc.Host]
	public void Load( string json )
	{
		CopiedJson = json;
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
		packagesReady = false;

		if ( string.IsNullOrWhiteSpace( CopiedJson ) )
			return;

		dupe = Json.Deserialize<DuplicationData>( CopiedJson );

		_ = InstallPackages( dupe );
	}

	async Task InstallPackages( DuplicationData data )
	{
		if ( data?.Packages is null || data.Packages.Count == 0 )
			return;

		foreach ( var pkg in data.Packages )
		{
			if ( Cloud.IsInstalled( pkg ) )
				continue;

			await Cloud.Load( pkg );
		}

		packagesReady = true;
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
			if ( model.Model.IsError )
			{
				var bounds = model.Bounds;
				if ( bounds.Size.IsNearlyZero() ) continue;

				var transform = tx.ToWorld( model.Transform );
				transform = new Transform( transform.PointToWorld( bounds.Center ), transform.Rotation, transform.Scale * (bounds.Size / 50) );
				DebugOverlay.Model( Model.Cube, transform: transform, overlay: false, materialOveride: overlayMaterial );
			}
			else
			{
				DebugOverlay.Model( model.Model, transform: tx.ToWorld( model.Transform ), overlay: false, materialOveride: overlayMaterial, localBoneTransforms: model.Bones );
			}
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
					var scl = entry["Scale"]?.Deserialize<Vector3>() ?? Vector3.One;

					var world = dest.ToWorld( new Transform( pos, rot ) );
					world.Scale = scl;

					var go = new GameObject( false );
					go.Deserialize( obj, new GameObject.DeserializeOptions { TransformOverride = world } );

					go.NetworkSpawn( true, null );

					undo.Add( go );
				}
			}
		} );
	}

	public static void FromStorage( Storage.Entry item )
	{
		var localPlayer = Player.FindLocalPlayer();
		var toolgun = localPlayer?.GetWeapon<Toolgun>();
		if ( !toolgun.IsValid() ) return;

		localPlayer.SwitchWeapon<Toolgun>();
		toolgun.SetToolMode( "Duplicator" );

		var toolmode = localPlayer.GetComponentInChildren<Duplicator>( true );

		// we don't have a duplicator tool!
		if ( toolmode is null ) return;

		var json = item.Files.ReadAllText( "/dupe.json" );
		toolmode.Load( json );
	}

	public static async Task FromWorkshop( Storage.QueryItem item )
	{
		var notice = Notices.AddNotice( "downloading", Color.Yellow, $"Installing {item.Title}..", 0 );
		notice?.AddClass( "progress" );

		var installed = await item.Install();

		notice?.Dismiss();

		if ( installed == null ) return;

		FromStorage( installed );
	}
}
