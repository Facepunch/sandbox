public static class ISpawnerExtensions
{
	extension( ISpawner spawner )
	{
		/// <summary>
		/// Create an <see cref="ISpawner"/> from its type, path, and optional metadata.
		/// The spawner may still be loading — await <see cref="ISpawner.Loading"/> before use.
		/// </summary>
		public static ISpawner Create( string type, string path, string source = null, string metadata = null )
		{
			return type switch
			{
				"prop" => new PropSpawner( path ),
				"mount" => new MountSpawner( path, metadata ),
				"entity" or "sent" => new EntitySpawner( path ),
				"dupe" => new DuplicatorSpawner( path, source ),
				_ => null
			};
		}
	}
}
