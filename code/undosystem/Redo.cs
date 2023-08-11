using Sandbox;

public class Redo
{
	public IClient Creator;
	public Entity Prop;
	public Vector3 Position;
	public Rotation Rotation;
	public Vector3 Velocity;
	public Undo Undo;
	public float Time;

	public bool IsDrawing;
	public bool IsPhysicsEnabled;

	public Redo( IClient creator, Entity prop, Undo undo )
	{
		Creator = creator;
		Prop = prop;
		Position = prop.Position;
		Rotation = prop.Rotation;
		Velocity = prop.Velocity;
		Undo = undo;
		Time = undo.Time;

		IsDrawing = prop.EnableDrawing;

		if ( prop is ModelEntity modelProp )
		{
			IsPhysicsEnabled = modelProp.PhysicsEnabled;
		}
	}
}
