using Sandbox;

public class EntityUndo : ICanUndo
{
	internal Entity entity { get; set; }

	public EntityUndo( Entity entity ) => this.entity = entity;

	public void DoUndo() => entity.Delete();

	public bool IsValidUndo() => ( entity != null && entity.IsValid() );
}
