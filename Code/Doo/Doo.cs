public partial class Doo
{
	public List<Block> Body { get; set; } = [];

	public enum InvokeType : byte
	{
		[Icon( "public" )]
		[Title( "Static Global" )]
		Static,

		[Icon( "inventory" )]
		[Title( "Component" )]
		Member,
	}

	public string GetLabel()
	{
		if ( Body?.Count <= 0 ) return "Empty";

		return $"{Body.Count} Commands";
	}

	public bool IsEmpty()
	{
		return Body == null || Body.Count == 0;
	}
}
