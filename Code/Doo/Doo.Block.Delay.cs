using System.Text.Json.Serialization;

public partial class Doo
{
	[Icon( "timer" )]
	public class DelayBlock : Block
	{
		[JsonIgnore]
		public override Color EditorColor => "#5C4837";

		[JsonInclude]
		public Expression Seconds { get; set; }

		public override string GetNodeString()
		{
			return Seconds == null ? "Delay (none)" : $"Delay {Seconds.GetDebugText()}s";
		}

		public override void Reset()
		{
			Seconds = new LiteralExpression() { LiteralValue = 1.0f };
		}
	}
}
