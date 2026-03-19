public partial class Doo
{
	internal class RunContext
	{
		public DooEngine Engine;
		public Doo Doo;
		public Component SourceComponent;
		public Dictionary<string, object> LocalVariables = new( StringComparer.OrdinalIgnoreCase );

		internal void Clear()
		{
			Doo = default;
			SourceComponent = default;
			LocalVariables.Clear();
		}
	}
}
