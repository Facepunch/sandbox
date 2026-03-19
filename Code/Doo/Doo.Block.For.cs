using System.Text.Json.Serialization;

public partial class Doo
{
	/// <summary>
	/// Run a block of code a certain number of times, with a loop variable.
	/// </summary>
	[Icon( "♾️", "#4f69db", "white" )]
	[Title( "For Block" )]
	public class ForBlock : Block
	{
		[JsonInclude]
		public string VariableName { get; set; } = "i";

		[JsonInclude]
		[TypeHint( typeof( int ) )]
		public Expression StartValue { get; set; }

		[JsonInclude]
		[TypeHint( typeof( int ) )]
		public Expression EndValue { get; set; }

		[JsonInclude]
		[TypeHint( typeof( int ) )]
		public Expression JumpValue { get; set; }

		public override bool HasBody() => true;

		public override string GetNodeString()
		{
			return $"for ( {VariableName} = {StartValue?.GetDebugText()}; {VariableName} < {EndValue?.GetDebugText()}; {VariableName} += {JumpValue?.GetDebugText()} )";
		}

		public override void Reset()
		{
			VariableName = "i";
			StartValue = new LiteralExpression { LiteralValue = 0 };
			EndValue = new LiteralExpression { LiteralValue = 10 };
			JumpValue = new LiteralExpression { LiteralValue = 1 };
			Body = new();
		}

		public override void CollectArguments( HashSet<string> arguments )
		{
			base.CollectArguments( arguments );

			if ( !string.IsNullOrWhiteSpace( VariableName ) )
			{
				arguments.Add( VariableName );
			}

			StartValue?.CollectArguments( arguments );
			EndValue?.CollectArguments( arguments );
			JumpValue?.CollectArguments( arguments );
		}
	}
}
