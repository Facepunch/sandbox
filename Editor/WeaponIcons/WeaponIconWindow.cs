using System;
using System.IO;
using System.Threading.Tasks;
using Sandbox.Resources;

namespace Sandbox.WeaponIcons;

/// <summary>
/// Renders an inventory item's model through hud_icon.vmat into a transparent 512x256 image and assigns it
/// </summary>
public class WeaponIconWindow : BaseWindow
{
	public const int IconWidth = 512;
	public const int IconHeight = 256;

	const string MaterialPath = "materials/default/hud_icon.vmat";
	const string OutputFolder = "ui/weapons"; // todo: allow changing this?

	const float FramePadding = 1.1f;

	static WeaponIconWindow _instance;

	readonly BaseInventoryItem Item;
	readonly SerializedProperty IconProperty;
	readonly Action OnSaved;
	readonly string IconName;

	WeaponIconSettings Settings;

	Scene Scene;
	CameraComponent Camera;
	ModelRenderer Renderer;
	HighlightOutline Outline;

	WeaponIconPreview Preview;
	Layout FormLayout;
	RealTimeSince _lastPreview;

	string CookieKey => $"WeaponIconCreator.{IconName}";
	string RelativeIconPath => $"{OutputFolder}/{IconName}_icon.png";

	public static void Open( BaseInventoryItem item, SerializedProperty iconProperty, Action onSaved = null )
	{
		if ( _instance.IsValid() )
			_instance.Destroy();

		_instance = new WeaponIconWindow( item, iconProperty, onSaved );
		_instance.Show();
	}

	WeaponIconWindow( BaseInventoryItem item, SerializedProperty iconProperty, Action onSaved )
	{
		Item = item;
		IconProperty = iconProperty;
		OnSaved = onSaved;
		IconName = GetIconName( item );
		Settings = LoadSettings();

		WindowTitle = $"Weapon Icon - {IconName}";
		SetWindowIcon( "add_photo_alternate" );

		Layout = Layout.Column();
		Layout.Margin = 20;
		Layout.Spacing = 16;

		CreateScene();

		Preview = Layout.Add( new WeaponIconPreview( this ) );
		FormLayout = Layout.AddColumn();
		RebuildForm();

		Layout.AddStretchCell();

		var buttons = Layout.AddRow();
		buttons.Add( new Editor.Button.Primary( "Save Icon", "save" ) { Clicked = () => _ = SaveIcon() } );
		buttons.AddStretchCell();
		buttons.Add( new Editor.Button( "Reset", "restart_alt" ) { Clicked = ResetSettings } );

		Size = new Vector2( IconWidth + 40, 600 );
		MinimumSize = Size;

		ApplySettings();
	}

	public override void OnDestroyed()
	{
		base.OnDestroyed();

		Scene?.Destroy();
		Scene = null;	}

	void CreateScene()
	{
		Scene = Scene.CreateEditorScene();
		Scene.Name = "Weapon Icon";

		using ( Scene.Push() )
		{
			var cameraObject = new GameObject( true, "camera" );
			Camera = cameraObject.AddComponent<CameraComponent>();
			Camera.Orthographic = true;
			Camera.BackgroundColor = Color.Transparent;
			Camera.IsMainCamera = true;
			Camera.ZNear = 1;
			cameraObject.AddComponent<Highlight>();

			var modelObject = new GameObject( true, "model" );
			Renderer = modelObject.AddComponent<ModelRenderer>();
			Renderer.MaterialOverride = Material.Load( MaterialPath );

			Outline = modelObject.AddComponent<HighlightOutline>();
		}
	}

	void RebuildForm()
	{
		FormLayout.Clear( true );

		var so = Settings.GetSerialized();
		so.OnPropertyChanged = _ => ApplySettings();

		var form = new Widget( this );
		form.Layout = Editor.ControlSheet.Create( so );
		FormLayout.Add( form );
	}

	void ApplySettings()
	{
		var model = Settings.Model;
		Renderer.Model = model;

		Outline.Color = Settings.OutlineColor;
		Outline.ObscuredColor = Settings.OutlineColor.WithAlpha( 0 );
		Outline.Width = Settings.OutlineWidth;

		if ( model is null || model.IsError )
			return;

		var rotation = Settings.Rotation.ToRotation();
		var bounds = model.Bounds;

		Renderer.WorldRotation = rotation;
		Renderer.WorldPosition = -(rotation * bounds.Center) + Settings.PositionOffset;

		var extents = Vector3.Zero;

		for ( int i = 0; i < 8; i++ )
		{
			var corner = new Vector3(
				(i & 1) == 0 ? bounds.Mins.x : bounds.Maxs.x,
				(i & 2) == 0 ? bounds.Mins.y : bounds.Maxs.y,
				(i & 4) == 0 ? bounds.Mins.z : bounds.Maxs.z );

			extents = Vector3.Max( extents, (rotation * (corner - bounds.Center)).Abs() );
		}

		var aspect = (float)IconWidth / IconHeight;
		var fitHeight = MathF.Max( extents.z * 2, extents.y * 2 / aspect ) * FramePadding;

		Camera.OrthographicHeight = fitHeight / MathF.Max( Settings.CameraZoom, 0.01f );
		Camera.WorldRotation = Rotation.Identity;
		Camera.WorldPosition = new Vector3( -(extents.x + MathF.Abs( Settings.PositionOffset.x ) + 64), 0, 0 );
		Camera.ZFar = (extents.x + MathF.Abs( Settings.PositionOffset.x )) * 2 + 128;
	}

	[EditorEvent.Frame]
	void Frame()
	{
		if ( Scene is null ) return;

		Scene.EditorTick( RealTime.Now, RealTime.Delta );

		if ( _lastPreview < 1f / 30f ) return;
		_lastPreview = 0;

		using var bitmap = RenderIcon();
		Preview.Pixmap = Pixmap.FromBitmap( bitmap );
		Preview.Update();
	}

	Bitmap RenderIcon()
	{
		var bitmap = new Bitmap( IconWidth, IconHeight );
		Camera.RenderToBitmap( bitmap, false );
		return bitmap;
	}

	async Task SaveIcon()
	{
		var absolutePath = Path.Combine( Project.Current.GetAssetsPath(), RelativeIconPath );
		Directory.CreateDirectory( Path.GetDirectoryName( absolutePath ) );

		Scene.EditorTick( RealTime.Now, RealTime.Delta );
		using ( var bitmap = RenderIcon() )
		{
			File.WriteAllBytes( absolutePath, bitmap.ToPng() );
		}

		AssetSystem.RegisterFile( absolutePath );

		ProjectCookie.Set( CookieKey, Settings );

		Log.Info( $"Saved weapon icon to {RelativeIconPath}" );

		if ( Item.IsValid() && IconProperty is not null )
		{
			var generator = new ImageFileGenerator { FilePath = RelativeIconPath };
			var texture = await generator.CreateAsync( ResourceGenerator.Options.Default, default );

			IconProperty.SetValue( texture );
			OnSaved?.Invoke();
		}

		Close();
	}

	void ResetSettings()
	{
		Settings = CreateDefaultSettings();
		RebuildForm();
		ApplySettings();
	}

	WeaponIconSettings LoadSettings()
	{
		if ( ProjectCookie.TryGet<WeaponIconSettings>( CookieKey, out var saved ) && saved is not null )
			return saved;

		return CreateDefaultSettings();
	}

	WeaponIconSettings CreateDefaultSettings()
	{
		var model = Item.IsValid()
			? Item.GetComponentsInChildren<ModelRenderer>( true ).Select( x => x.Model ).FirstOrDefault( x => x is not null && !x.IsError )
			: null;

		return new WeaponIconSettings { Model = model };
	}

	/// <summary>
	/// Named after the item's prefab file ("weapons/grenade/grenade.prefab" -> "grenade"), which is
	/// unique and doesn't change with language or display name tweaks.
	/// </summary>
	static string GetIconName( BaseInventoryItem item )
	{
		var name = Path.GetFileNameWithoutExtension( GetPrefabPath( item ) ?? "" );

		if ( string.IsNullOrWhiteSpace( name ) )
			name = item.GameObject.Name;

		var clean = new string( name.ToLowerInvariant().Select( c => char.IsLetterOrDigit( c ) ? c : '_' ).ToArray() ).Trim( '_' );
		return string.IsNullOrEmpty( clean ) ? "item" : clean;
	}

	/// <summary>
	/// The nearest prefab instance the item lives in when it's placed in a scene, or the prefab
	/// itself when it's open in the prefab editor.
	/// </summary>
	static string GetPrefabPath( BaseInventoryItem item )
	{
		for ( var go = item.GameObject; go.IsValid() && go is not Sandbox.Scene; go = go.Parent )
		{
			if ( !string.IsNullOrEmpty( go.PrefabInstanceSource ) )
				return go.PrefabInstanceSource;
		}

		return item.Scene is PrefabScene prefabScene ? prefabScene.Source?.ResourcePath : null;
	}
}

internal class WeaponIconPreview : Widget
{
	const int CheckerSize = 16;

	public Pixmap Pixmap { get; set; }

	public WeaponIconPreview( Widget parent ) : base( parent )
	{
		FixedSize = new Vector2( WeaponIconWindow.IconWidth, WeaponIconWindow.IconHeight );
	}

	protected override void OnPaint()
	{
		Paint.ClearPen();

		for ( int y = 0; y < Height; y += CheckerSize )
		{
			for ( int x = 0; x < Width; x += CheckerSize )
			{
				var odd = ((x + y) / CheckerSize) % 2 == 1;
				Paint.SetBrush( odd ? new Color( 0.2f, 0.2f, 0.2f ) : new Color( 0.16f, 0.16f, 0.16f ) );
				Paint.DrawRect( new Rect( x, y, CheckerSize, CheckerSize ) );
			}
		}

		if ( Pixmap is not null )
		{
			Paint.Draw( LocalRect, Pixmap );
		}
	}
}
