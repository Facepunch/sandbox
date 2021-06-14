using Sandbox;

public class Redo
{
	public Entity Creator;
	public Entity Prop;
	public Vector3 Pos;
	public Rotation Rotation;
	public Vector3 Velocity;
	public Undo Undo;
	public float Time;

	public Redo( Entity creator, Entity prop, Vector3 pos, Rotation rot, Vector3 vel, Undo undo, float time )
	{
		Creator = creator;
		Prop = prop;
		Pos = pos;
		Rotation = rot;
		Velocity = vel;
		Undo = undo;
		Time = time;
	}
}
