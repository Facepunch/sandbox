
[Icon( "🔗" )]
[Title( "#tool.name.linker" )]
[ClassName( "linker" )]
[Group( "#tool.group.constraints" )]
public class LinkerTool : BaseConstraintToolMode
{
	public override string Description => Stage == 1 ? "#tool.hint.linker.stage1" : "#tool.hint.linker.stage0";
	public override string PrimaryAction => Stage == 1 ? "#tool.hint.linker.finish" : "#tool.hint.linker.source";
	public override string ReloadAction => "#tool.hint.linker.remove";

	protected override IEnumerable<GameObject> FindConstraints( GameObject linked, GameObject target )
	{
		foreach ( var link in linked.GetComponentsInChildren<ManualLink>( true ) )
			if ( linked == target || link.Body?.Root == target )
				yield return link.GameObject;
	}

	protected override void CreateConstraint( SelectionPoint point1, SelectionPoint point2 )
	{
		var go1 = new GameObject( point1.GameObject, false, "link" );
		var go2 = new GameObject( point2.GameObject, false, "link" );

		var link1 = go1.AddComponent<ManualLink>();
		var link2 = go2.AddComponent<ManualLink>();

		link1.Body = go2;
		link2.Body = go1;

		go2.NetworkSpawn();
		go1.NetworkSpawn();

		Track( go1, go2 );

		var undo = Player.Undo.Create();
		undo.Name = "Link";
		undo.Add( go1 );
	}
}

