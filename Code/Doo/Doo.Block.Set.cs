using System.Text.Json.Serialization;

public partial class Doo
{
	/// <summary>
	/// Set a variable to a value.
	/// </summary>
	[Icon( "📥", "#6b336c", "white" )]
	[Title( "Set Variable" )]
	public class SetBlock : Block
	{
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
