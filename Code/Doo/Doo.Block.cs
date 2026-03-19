using System.Text.Json.Serialization;

public partial class Doo
{

	[JsonDerivedType( typeof( DelayBlock ), "del" )]
	[JsonDerivedType( typeof( SetBlock ), "set" )]
	[JsonDerivedType( typeof( InvokeBlock ), "ivk" )]
	[JsonDerivedType( typeof( ReturnBlock ), "ret" )]
	[JsonDerivedType( typeof( ForBlock ), "for" )]
	public partial class Block
	{
		[JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		public List<Block> Body { get; set; }

		public virtual string GetNodeString()
		{
			return "unhandled block";
		}

		/// <summary>
		/// Returns true if this can have child nodes
		/// </summary>
		public virtual bool HasBody() => false;

		/// <summary>
		/// Reset this block to some sensible defaults. This is called when 
		/// the block is first added, so this is a good opportunity to set up default 
		/// values for properties.
		/// </summary>
		public virtual void Reset()
		{

		}

		public virtual void CollectArguments( HashSet<string> arguments )
		{
			if ( Body == null ) return;

			foreach ( var b in Body )
			{
				b.CollectArguments( arguments );
			}
		}
	}
}
