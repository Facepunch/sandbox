public static class ISpawnerExtensions
{
	extension( ISpawner spawner )
	{
		/// <summary>
		/// Create an <see cref="ISpawner"/> from its type, path, and optional metadata.
		/// </summary>
		public static async Task<ISpawner> Create( string type, string path, string source = null, string metadata = null )
		{
			ISpawner result = type switch
			{
				"prop" => new PropSpawner( path ),
				"mount" => new MountSpawner( path, metadata ),
				"entity" or "sent" => new EntitySpawner( path ),
				"dupe" => await CreateDupe( path, source ),
				_ => null
			};

			if ( result is not null && !await result.Loading )
				return null;

			return result;
		}

		private static async Task<DuplicatorSpawner> CreateDupe( string id, string source )
		{
			if ( !ulong.TryParse( id, out var fileId ) )
				return null;

			if ( source == "workshop" )
			{
				var query = new Storage.Query { FileIds = [fileId] };

				var result = await query.Run();
				var item = result.Items?.FirstOrDefault();
				if ( item is null ) return null;

				var installed = await item.Install();
				if ( installed is null ) return null;

				var json = await installed.Files.ReadAllTextAsync( "/dupe.json" );
				return DuplicatorSpawner.FromJson( json, item.Title );
			}

			var entry = Storage.GetAll( "dupe" ).FirstOrDefault( x => x.Id.ToString() == fileId.ToString() );
			if ( entry is null ) return null;

			var dupeJson = await entry.Files.ReadAllTextAsync( "/dupe.json" );
			return DuplicatorSpawner.FromJson( dupeJson, entry.GetMeta<string>( "name" ) );
		}
	}
}
