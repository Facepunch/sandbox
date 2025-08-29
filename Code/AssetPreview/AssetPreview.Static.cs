namespace Sandbox.Utility;

public sealed partial class AssetPreview
{
	public static Texture GetIcon( string path )
	{
		string cacheKey = $"preview/icons/{path.ToLower()}";

		if ( FileSystem.Cache.TryGet( cacheKey, out var data ) )
		{
			var bitmap = Bitmap.CreateFromBytes( data );
			return bitmap.ToTexture();
		}

		var job = new RenderJob( path, cacheKey );
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

		string cacheKey;

		public RenderJob( string path, string cacheKey )
		{
			this.cacheKey = cacheKey;
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

			var bitmap = new Bitmap( 512, 512 );
			SceneUtility.RenderModelBitmap( modelResource, bitmap );

			bitmap = bitmap.Resize( 256, 256 );
			Texture.Update( bitmap );

			FileSystem.Cache.Set( cacheKey, bitmap.ToPng() );
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
