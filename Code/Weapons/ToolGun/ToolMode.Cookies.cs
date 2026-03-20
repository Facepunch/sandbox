partial class ToolMode : ICookieSource
{
	public virtual string CookiePrefix => $"tool.{GetType().Name.ToLower()}";
}
