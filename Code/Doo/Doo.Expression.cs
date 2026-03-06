using System.Text.Json.Serialization;

public partial class Doo
{
	[Icon( "tag" )]
	public class LiteralExpression : Expression
	{
		[JsonInclude]
		public Variant LiteralValue { get; set; }

		public override Variant Evaluate() => LiteralValue;
		public override string GetDebugText() => LiteralValue.ToString();
	}

	[Icon( "abc" )]
	public class VariableExpression : Expression
	{
		[JsonInclude]
		[JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		public string VariableName { get; set; }

		public override Variant Evaluate() => default;
		public override string GetDebugText() => $"{VariableName}";
	}

	[JsonDerivedType( typeof( LiteralExpression ), "lit" )]
	[JsonDerivedType( typeof( VariableExpression ), "var" )]
	public abstract class Expression
	{
		public virtual Variant Evaluate() => default;
		public virtual string GetDebugText() => "";
	}
}
