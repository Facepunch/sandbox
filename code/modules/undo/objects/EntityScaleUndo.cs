using Sandbox;

public class EntityScaleUndo : ICanUndo
{
	internal Entity entity { get; set; }
	internal float scale { get; set; }

	public EntityScaleUndo( Entity entity, float scale )
	{
		this.entity = entity;
		this.scale = scale;
	}

	public void DoUndo()
	{
		entity.Scale = scale;
		entity.PhysicsGroup.RebuildMass();
		entity.PhysicsGroup.Wake();

		foreach ( var child in entity.Children )
		{
			if ( !child.IsValid() )
				continue;

			child.PhysicsGroup.RebuildMass();
			child.PhysicsGroup.Wake();
		}
	}

	public bool IsValidUndo() => entity != null && entity.IsValid();
}
