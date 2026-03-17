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
		return $"Contains {Body.Count} Steps";
	}
}
