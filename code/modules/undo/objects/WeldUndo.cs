using Sandbox;

public class WeldUndo : ICanUndo
{
	internal Prop parent { get; set; }
	internal Prop child { get; set; }

	public WeldUndo( Prop parent, Prop child )
	{
		this.parent = parent;
		this.child = child;
	}

	public void DoUndo() => child.Unweld( true, parent );

	public bool IsValidUndo() => parent != null && parent.IsValid() && child != null && child.IsValid();
}
