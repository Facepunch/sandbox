using Sandbox;

public class Undo
{
	public Entity Creator;
	public Entity Prop;
	public Vector3 Velocity;
	public float Time;
	public bool Avoid;

	public Undo( Entity creator, Entity prop )
	{
		Creator = creator;
		Prop = prop;
		Velocity = prop.Velocity;
		Time = Sandbox.Time.Now;
		Avoid = false;
	}
}
