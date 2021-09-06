using Sandbox;

public class ModelColorUndo : ICanUndo
{
	internal ModelEntity modelEntity { get; set; }
	internal Color32 pastColor { get; set; }

	public ModelColorUndo( ModelEntity modelEntity, Color32 pastColor )
	{
		this.modelEntity = modelEntity;
		this.pastColor = pastColor;
	}

	public void DoUndo() => modelEntity.RenderColor = pastColor;

	public bool IsValidUndo() => modelEntity != null && modelEntity.IsValid() && modelEntity.RenderColor != pastColor;
}
