﻿using Sandbox.UI;
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

	DuplicatorSpawner spawner;
	LinkedGameObjectBuilder builder = new();

	public override string Description => "#tool.hint.duplicator.description";
	public override string PrimaryAction => spawner is not null ? "#tool.hint.duplicator.place" : null;
	public override string SecondaryAction => "#tool.hint.duplicator.copy";

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		IsValidState = IsValidTarget( select );

		if ( spawner is { IsReady: true } && Input.Pressed( "attack1" ) )
		{
			if ( !IsValidPlacementTarget( select ) )
			{
				// make invalid noise
				return;
			}

			var tx = new Transform();
			tx.Position = select.WorldPosition() + Vector3.Down * spawner.Bounds.Mins.z;

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
		spawner = null;

		if ( string.IsNullOrWhiteSpace( CopiedJson ) )
			return;

		spawner = DuplicatorSpawner.FromJson( CopiedJson );
	}

	void DrawPreview()
	{
		if ( spawner is null ) return;

		var select = TraceSelect();
		if ( !IsValidPlacementTarget( select ) ) return;

		var tx = new Transform();

		tx.Position = select.WorldPosition() + Vector3.Down * spawner.Bounds.Mins.z;

		var relative = Player.EyeTransform.Rotation.Angles();
		tx.Rotation = new Angles( 0, relative.yaw, 0 );

		var overlayMaterial = IsProxy ? Material.Load( "materials/effects/duplicator_override_other.vmat" ) : Material.Load( "materials/effects/duplicator_override.vmat" );
		spawner.DrawPreview( tx, overlayMaterial );
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
	public async void Duplicate( Transform dest )
	{
		if ( spawner is null )
			return;

		var player = Player.FindForConnection( Rpc.Caller );
		if ( player is null ) return;

		var objects = await spawner.Spawn( dest, player );

		if ( objects is { Count: > 0 } )
		{
			var undo = player.Undo.Create();
			undo.Name = "Duplication";

			foreach ( var go in objects )
			{
				undo.Add( go );
			}
		}
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
