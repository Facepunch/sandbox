/// <summary>
/// Hardcoded models that ship with the game and aren't available on the workshop.
/// Add new entries here — they'll appear automatically in the Local props section.
/// </summary>
public static class LocalProps
{
	public record Entry( string Path, string Category )
	{
		/// <summary>
		/// Derives a display name from the filename: strips the extension,
		/// replaces underscores with spaces, and title-cases each word.
		/// </summary>
		public string DisplayName
		{
			get
			{
				var file = System.IO.Path.GetFileNameWithoutExtension( Path );
				// strip a second extension for compiled paths like .vmdl_c
				file = System.IO.Path.GetFileNameWithoutExtension( file );
				var words = file.Split( '_' );
				return string.Join( " ", words.Select( w => char.ToUpperInvariant( w[0] ) + w[1..] ) );
			}
		}
	}

	public static List<Entry> All => new()
	{
		// Characters
		new( "models/citizen_mannequin/mannequin.vmdl", "characters" ),
		new( "models/citizen/citizen.vmdl", "characters" ),
		new( "models/citizen_human/citizen_human_male.vmdl", "characters" ),
		new( "models/citizen_human/citizen_human_female.vmdl", "characters" ),

		// Props (citizen_props)
		new( "models/citizen_props/beachball.vmdl", "props" ),
		new( "models/citizen_props/broom01.vmdl", "props" ),
		new( "models/citizen_props/cardboardbox01.vmdl", "props" ),
		new( "models/citizen_props/chair01.vmdl", "props" ),
		new( "models/citizen_props/chair02.vmdl", "props" ),
		new( "models/citizen_props/chair03.vmdl", "props" ),
		new( "models/citizen_props/chair04blackleather.vmdl", "props" ),
		new( "models/citizen_props/chair05bluefabric.vmdl", "props" ),
		new( "models/citizen_props/coffeemug01.vmdl", "props" ),
		new( "models/citizen_props/concreteroaddivider01.vmdl", "props" ),
		new( "models/citizen_props/crate01.vmdl", "props" ),
		new( "models/citizen_props/crowbar01.vmdl", "props" ),
		new( "models/citizen_props/gritbin01_combined.vmdl", "props" ),
		new( "models/citizen_props/newspaper01.vmdl", "props" ),
		new( "models/citizen_props/oldoven.vmdl", "props" ),
		new( "models/citizen_props/recyclingbin01.vmdl", "props" ),
		new( "models/citizen_props/roadcone01.vmdl", "props" ),
		new( "models/citizen_props/sodacan01.vmdl", "props" ),
		new( "models/citizen_props/trashbag02.vmdl", "props" ),
		new( "models/citizen_props/trashcan01.vmdl", "props" ),
		new( "models/citizen_props/trashcan02.vmdl", "props" ),
		new( "models/citizen_props/wheel01.vmdl", "props" ),
		new( "models/citizen_props/wheel02.vmdl", "props" ),
		new( "models/citizen_props/wineglass01.vmdl", "props" ),
		new( "models/citizen_props/wineglass02.vmdl", "props" ),
	};
}
