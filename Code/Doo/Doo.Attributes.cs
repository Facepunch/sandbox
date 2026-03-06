using System;

public partial class Doo
{
	[AttributeUsage( AttributeTargets.Method )]
	public sealed class StaticMethodAttribute : System.Attribute
	{
		public string Path { get; set; }

		public string CategoryName { get; init; }

		public StaticMethodAttribute( string path )
		{
			Path = path;

			var paths = Path.Split( '.' );
			CategoryName = paths.FirstOrDefault();
		}
	}

	/// <summary>
	/// Hint that this type is expected to be this. This is used internally for
	/// the editor UX to hint that a type of a Value should be a specific type.
	/// </summary>
	public sealed class TypeHintAttribute : System.Attribute
	{
		public Type Hint { get; set; }
		public TypeHintAttribute( Type hint )
		{
			Hint = hint;
		}
	}

	/// <summary>
	/// Specify a hint on a Doo explaining that we're going to be passing in an expected argument when calling it.
	/// </summary>
	public class ArgumentHintAttribute : System.Attribute
	{
		public string Name { get; set; }
		public string Help { get; set; }
		public Type Hint { get; set; }
	}

	/// <summary>
	/// Specify a hint on a Doo explaining that we're going to be passing in an expected argument when calling it.
	/// </summary>
	public sealed class ArgumentHintAttribute<T> : ArgumentHintAttribute
	{
		public ArgumentHintAttribute( string name )
		{
			Name = name;
			Hint = typeof( T );
		}
	}
}
