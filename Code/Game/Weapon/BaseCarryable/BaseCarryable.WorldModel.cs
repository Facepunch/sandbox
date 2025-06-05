using Sandbox.Citizen;

public partial class BaseCarryable : Component
{
	public interface IEvent : ISceneEvent<IEvent>
	{
		public void OnCreateWorldModel() { }
		public void OnDestroyWorldModel() { }
	}

	[Property, Feature( "WorldModel" )] public GameObject WorldModelPrefab { get; set; }
	[Property, Feature( "WorldModel" )] public CitizenAnimationHelper.HoldTypes HoldType { get; set; } = CitizenAnimationHelper.HoldTypes.HoldItem;
	[Property, Feature( "WorldModel" )] public string ParentBone { get; set; } = "hold_r";

	protected void CreateWorldModel()
	{
		DestroyWorldModel();

		if ( WorldModelPrefab is null )
			return;

		var player = GetComponentInParent<PlayerController>();
		if ( player is null || player.Renderer is null ) return;

		var parentBone = player.Renderer.GetBoneObject( ParentBone );

		WorldModel = WorldModelPrefab.Clone( new CloneConfig { Parent = parentBone, StartEnabled = false, Transform = global::Transform.Zero } );
		WorldModel.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;
		WorldModel.Enabled = true;

		IEvent.PostToGameObject( WorldModel, x => x.OnCreateWorldModel() );
	}

	protected void DestroyWorldModel()
	{
		if ( WorldModel.IsValid() )
		{
			IEvent.PostToGameObject( WorldModel, x => x.OnDestroyWorldModel() );
		}

		WorldModel?.Destroy();
		WorldModel = default;
	}
}
