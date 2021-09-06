using Sandbox;

public class PhysicsJointUndo : ICanUndo
{
	internal PhysicsJoint physicsJoint { get; set; }

	public PhysicsJointUndo( PhysicsJoint physicsJoint ) => this.physicsJoint = physicsJoint;

	public void DoUndo() => physicsJoint.Remove();

	public bool IsValidUndo() => physicsJoint != null && physicsJoint.IsValid();
}
