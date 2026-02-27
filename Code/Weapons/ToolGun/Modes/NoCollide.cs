﻿
[Icon( "⛔" )]
[Title( "No Collide" )]
[ClassName( "nocollide" )]
[Group( "Tools" )]
public class NoCollide : BaseConstraintToolMode
{
	public override string Description => Stage == 1 ? "#tool.hint.nocollide.stage1" : "#tool.hint.nocollide.stage0";
	public override string PrimaryAction => Stage == 1 ? "#tool.hint.nocollide.finish" : "#tool.hint.nocollide.source";

	protected override void CreateConstraint( SelectionPoint point1, SelectionPoint point2 )
	{
		var go = new GameObject( point1.GameObject, false, "no collide" );
		var joint = go.AddComponent<PhysicsFilter>();
		joint.Body = point2.GameObject;

		go.NetworkSpawn();

		var undo = Player.Undo.Create();
		undo.Name = "No Collide";
		undo.Add( go );
	}
}
