namespace Sandbox
{
	public partial class MeshEntity : Prop
	{
		[Net]
		public string ModelId { get; set; }
		[Net]
		public string MaterialOverride { get; set; } = "";
		public Model VertexModel => VertexMeshBuilder.Models[ModelId];

		private string _lastModel;
		private string _lastMaterial;

		[GameEvent.Tick]
		public void Tick()
		{
			if ( ModelId == "" || ModelId == null || !VertexMeshBuilder.Models.ContainsKey( ModelId ) ) {
				return; // happens after a hot reload :()
			}
			if ( ModelId != "" && ModelId != _lastModel ) {
				Model = VertexModel;
				SetupPhysicsFromModel( PhysicsMotionType.Dynamic );

				_lastModel = ModelId;
				_lastMaterial = "";
			}
			if ( Game.IsClient && MaterialOverride != null && MaterialOverride != "" && _lastMaterial != MaterialOverride ) {
				SceneObject.SetMaterialOverride( Material.Load( MaterialOverride ) );
				_lastMaterial = MaterialOverride;
			}
		}
	}
}
