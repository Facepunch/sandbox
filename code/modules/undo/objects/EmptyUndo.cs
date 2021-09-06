public class EmptyUndo : ICanUndo
{
	public void DoUndo() { }

	public bool IsValidUndo() => true;
}
