public partial class Doo
{
	internal class RunContext
	{
		public DooEngine Engine;
		public Doo Doo;
		public BaseDooComponent SourceComponent;
		public Dictionary<string, object> LocalVariables = new( StringComparer.OrdinalIgnoreCase );
		public Task Task;
		public bool Stopped;

		internal void Clear()
		{
			Doo = default;
			SourceComponent = default;
			LocalVariables.Clear();
			Task = default;
			Stopped = false;
		}
	}
}
