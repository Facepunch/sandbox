using System.Text.Json.Serialization;

public partial class Doo
{
	[Icon( "trending_flat" )]
	public class SetBlock : Block
	{
		[JsonIgnore]
		public override Color EditorColor => "#6b336c";

		[JsonInclude]
		public string VariableName { get; set; }

		[JsonInclude]
		public Expression Value { get; set; }

		public override string GetNodeString()
		{
			return $"{VariableName} = {Value?.GetDebugText()}";
		}

		public override void Reset()
		{
			VariableName = "x";
			Value = new LiteralExpression { LiteralValue = "hello" };
		}

		public override void CollectArguments( HashSet<string> arguments )
		{
			base.CollectArguments( arguments );

			if ( VariableName == null ) return;
			arguments.Add( VariableName );
		}
	}
}
