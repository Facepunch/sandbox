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
		var player = GetComponentInParent<PlayerController>();
		if ( player is null || player.Renderer is null ) return;

		CreateWorldModel( player.Renderer, ParentBone );
	}

	public GameObject CreateWorldModel( SkinnedModelRenderer renderer, string boneName = "hold_r" )
	{
		DestroyWorldModel();

		if ( WorldModelPrefab is null )
			return null;

		if ( !renderer.IsValid() )
			return null;

		var bone = renderer.GetBoneObject( boneName ) ?? GameObject;

		var worldModel = WorldModelPrefab.Clone( new CloneConfig
		{
			Parent = bone,
			StartEnabled = false,
			Transform = global::Transform.Zero
		} );

		worldModel.Enabled = true;
		worldModel.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;

		// Track on the weapon so other systems can reference it.
		WorldModel = worldModel;

		IEvent.PostToGameObject( WorldModel, x => x.OnCreateWorldModel() );

		return worldModel;
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
