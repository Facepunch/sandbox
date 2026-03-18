/// <summary>
/// Links a <see cref="UtilityPage"/> to the <see cref="UtilityFunction"/> it belongs to.
/// </summary>
[AttributeUsage( AttributeTargets.Class )]
public class UtilityOfAttribute : Attribute
{
	public Type FunctionType { get; }

	public UtilityOfAttribute( Type functionType )
	{
		FunctionType = functionType;
	}
}
