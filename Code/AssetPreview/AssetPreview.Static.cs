namespace Sandbox.Utility;

public sealed partial class AssetPreview
{
	public static Texture GetIcon( string path )
	{
		// find in cache

		var job = new RenderJob( path );
		_jobs.Enqueue( job );
		return job.Texture;
	}

	static Queue<RenderJob> _jobs = new();
	static HashSet<RenderJob> _activeJobs = new();


	public class RenderJob
	{
		public string Path { get; }
		public Texture Texture { get; set; }

		Task task;

		public bool IsFinished => task is null || task.IsCompleted;

		public RenderJob( string path )
		{
			Path = path;

			var bitmap = new Bitmap( 256, 256 );
			bitmap.Clear( Color.Transparent );
			Texture = bitmap.ToTexture();
		}

		public void Start()
		{
			task = Run();
		}

		public async Task Run()
		{
			var modelResource = await Model.LoadAsync( Path );
			if ( !modelResource.IsValid() )
			{
				// make error icon?
				return;
			}

			var bitmap = new Bitmap( 1024, 1024 );
			bitmap.Clear( Color.Random );

			var scene = Scene.CreateEditorScene();

			using ( scene.Push() )
			{
				var cam = new GameObject( "camera" ).AddComponent<CameraComponent>();
				cam.BackgroundColor = Color.Transparent;
				cam.WorldRotation = new Angles( 15, 180 + 35, 0 );
				cam.FieldOfView = 30;

				var c = new GameObject( "envmap" ).AddComponent<EnvmapProbe>();
				c.Texture = Texture.Load( "textures/cubemaps/default2.vtex" );
				c.Bounds = BBox.FromPositionAndSize( Vector3.Zero, 1000 );


				var sun = new GameObject( "sun" ).AddComponent<DirectionalLight>();
				sun.WorldRotation = new Angles( 90, 0, 0 );
				sun.LightColor = Color.White * 2;
				sun.SkyColor = new Color( 0.4f, 0.5f, 0.5f );

				var target = new GameObject( "target" );
				var model = target.AddComponent<ModelRenderer>();
				model.Model = modelResource;

				var bounds = model.Bounds;

				{
					var distance = MathX.SphereCameraDistance( bounds.Size.Length * 0.5f, cam.FieldOfView );
					var aspect = (float)bitmap.Width / (float)bitmap.Height;
					if ( aspect > 1 ) distance *= aspect;

					cam.WorldPosition = bounds.Center + cam.WorldRotation.Forward * -distance;
				}

				var sideLight = new GameObject( "sidelight" ).AddComponent<PointLight>();
				sideLight.WorldPosition = new Vector3( 1000, 0, 0 );
				sideLight.LightColor = new Color( 0.1f, 1, 1 ) * 100;
				sideLight.Radius = 10000;

				cam.RenderToBitmap( bitmap );

			}

			scene.Destroy();

			Log.Info( $"Texture.Update {Texture.LastUsed}" );
			Texture.Update( bitmap.Resize( 256, 256 ) );
		}
	}
	public static void RunJobs()
	{
		_activeJobs.RemoveWhere( x => x.IsFinished );

		while ( _jobs.Count > 0 && _activeJobs.Count < 2 )
		{
			var job = _jobs.Dequeue();
			_activeJobs.Add( job );

			job.Start();
		}
	}
}
