using System;
using Sandbox;

public class Undo
{
	public IClient Creator;
	public Entity Prop;
	public Func<string> Undoable;
	public float Time;
	public bool Avoid;

	public Undo( IClient creator )
	{
		Creator = creator;
		Time = Sandbox.Time.Now;
		Avoid = false;
	}
	public Undo( IClient creator, Entity prop ) : this( creator )
	{
		Prop = prop;
	}
}
