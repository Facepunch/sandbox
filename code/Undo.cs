using Sandbox;

public class Undo
{
	public Entity Creator;
	public Entity Prop;
	public float Time;
	public bool Avoid;

	public Undo( Entity creator, Entity prop )
	{
		Creator = creator;
		Prop = prop;
		Time = Sandbox.Time.Now;
		Avoid = false;
	}
}
