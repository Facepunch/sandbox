using System.Text.Json.Serialization;

public partial class Doo
{
	/// <summary>
	/// Call a global method or a method on a component.
	/// </summary>
	[Icon( "🏃‍", "#506C33", "white" )]
	[Title( "Invoke" )]
	public class InvokeBlock : Block
	{
		[JsonInclude]
		public InvokeType InvokeType { get; set; }

		[JsonInclude]
		[JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		public Component TargetComponent { get; set; }

		[JsonInclude]
		public string Member { get; set; }

		[JsonInclude]
		[JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		public List<Expression> Arguments { get; set; }

		/// <summary>
		/// Variable name to set to the returned value. Leave empty to ignore the return value.
		/// </summary>
		[JsonInclude]
		[Title( "Variable" )]
		public string ReturnVariable { get; set; }

		public override string GetNodeString()
		{
			if ( Member == null ) return "(empty)";

			string targetName = null;

			if ( InvokeType == InvokeType.Member )
			{
				targetName = TargetComponent?.GameObject?.Name;
				if ( targetName == null ) return "(empty)";
			}

			var md = Doo.Helpers.FindMethod( Member );
			if ( md == null )
			{
				return $"Couldn't Find {Member}";
			}

			var funcName = md.Name;

			var attr = md.GetCustomAttribute<Doo.StaticMethodAttribute>();
			if ( attr != null ) funcName = attr.Path;

			var args = string.Join( ", ", Arguments?.Select( a => a?.GetDebugText() ) ?? Array.Empty<string>() );
			if ( args.Length > 1 ) args = $" {args} ";

			var funcTitle = $"{funcName}({args})";
			var returnValue = "";

			if ( md.ReturnType != typeof( void ) && !string.IsNullOrWhiteSpace( ReturnVariable ) )
			{
				returnValue = $"{ReturnVariable} = ";
			}

			if ( targetName != null )
			{
				return $"{returnValue}[{targetName}].{funcTitle}";
			}
			else
			{
				return $"{returnValue}{funcTitle}";
			}
		}

		public override void CollectArguments( HashSet<string> arguments )
		{
			base.CollectArguments( arguments );

			if ( !string.IsNullOrWhiteSpace( ReturnVariable ) )
			{
				arguments.Add( ReturnVariable );
			}
		}
	}
}
