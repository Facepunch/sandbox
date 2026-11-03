using Sandbox.UI;

[Hide, Title( "#tool.name.camera" ), Icon( "📷" ), ClassName( "cameratool" ), Group( "#tool.group.building" )]
public sealed class CameraTool : ToolMode
{
	public const string CameraPrefab = "entities/camera/camera_entity.prefab";

	[Property, Title( "#tool.setting.camera_key" ), Description( "#tool.setting.camera_key.description" ), Group( "#tool.setting.group.inputs" )]
	public ClientInput ViewInput { get; set; }

	[Property, Range( 1, 150 ), Title( "#tool.setting.camera_fov" ), Group( "#tool.setting.group.behavior" )]
	public float Fov { get; set; } = 75f;

	[Property, Title( "#tool.setting.camera_static" ), Description( "#tool.setting.camera_static.description" ), Group( "#tool.setting.group.behavior" )]
	public bool Static { get; set; }

	[Property, Title( "#tool.setting.camera_toggle" ), Description( "#tool.setting.camera_toggle.description" ), Group( "#tool.setting.group.inputs" )]
	public bool ToggleMode { get; set; } = true;

	public override string Description => "#tool.hint.camera.description";

	protected override void OnStart()
	{
		base.OnStart();
		RegisterAction( ToolInput.Primary, () => "#tool.hint.camera.place", Place );
	}

	void Place()
	{
		var view = Scene.Camera.IsValid() ? Scene.Camera.View.Transform : Player.EyeTransform;
		Spawn( view, WriteConfiguration<CameraEntity>() );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	public void Spawn( Transform view, string configuration )
	{
		if ( !Player.IsValid() || !FireToolAction( ToolInput.Primary ) ) return;
		var prefab = ResourceLibrary.Get<PrefabFile>( CameraPrefab );
		if ( prefab?.GetScene() is not Scene scene ) return;
		var go = scene.Clone( new CloneConfig { StartEnabled = false } );
		var entity = go.GetComponent<CameraEntity>( true );
		if ( !entity.IsValid() ) { go.Destroy(); return; }
		ApplyConfiguration( entity, configuration );
		entity.AlignView( view );
		go.Tags.Add( "removable" );
		Ownable.Set( go, Player.Network.Owner );
		var body = go.GetComponent<Rigidbody>( true );
		if ( body.IsValid() ) body.MotionEnabled = false;
		entity.RefreshPhysics();
		go.NetworkSpawn( true, null );
		Track( go );
		var undo = Player.Undo.Create();
		undo.Name = "#tool.name.camera";
		undo.Icon = "📷";
		undo.Add( go );
		FirePostToolAction( ToolInput.Primary );
	}
}
