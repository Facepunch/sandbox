using Sandbox.Citizen;

public partial class BaseSandboxWeapon
{
	public interface IEvent : ISceneEvent<IEvent>
	{
		public void OnCreateWorldModel() { }
		public void OnDestroyWorldModel() { }
	}

	// WorldModelPrefab, HoldBone and HoldType are inherited from the engine BaseWeapon.
	[Property, Feature( "WorldModel" )] public GameObject DroppedGameObject { get; set; }

	protected override void CreateWorldModel()
	{
		var player = GetComponentInParent<PlayerController>();
		if ( player?.Renderer is null ) return;

		CreateWorldModel( player.Renderer );
	}

	
	/// <summary>
	/// Enables or disables the physics/dropped components of this carryable.
	/// Call with <c>false</c> when picking up/holding, <c>true</c> when dropping.
	/// </summary>
	public void SetDropped( bool dropped )
	{
		var rb = GetComponent<Rigidbody>( true );
		if ( rb.IsValid() ) rb.Enabled = dropped;

		var col = GetComponent<ModelCollider>( true );
		if ( col.IsValid() ) col.Enabled = dropped;

		var droppedWeapon = GetComponent<DroppedWeapon>( true );
		if ( droppedWeapon.IsValid() ) droppedWeapon.Enabled = dropped;

		if ( DroppedGameObject.IsValid() ) DroppedGameObject.Enabled = dropped;
	}

	/// <summary>
	/// Creates and attaches the world model to the given renderer's bone.
	/// Use this overload when the weapon is held by something other than a player (e.g. an NPC).
	/// </summary>
	public void CreateWorldModel( SkinnedModelRenderer renderer )
	{
		if ( renderer is null ) return;

		if ( Networking.IsHost )
		{
			IsItem = false;
		}

		SetDropped( false );

		var worldModel = WorldModelPrefab?.Clone( new CloneConfig
		{
			Parent = renderer.GetBoneObject( HoldBone ) ?? GameObject,
			StartEnabled = true,
			Transform = global::Transform.Zero
		} );
		if ( worldModel.IsValid() )
		{
			worldModel.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;
			WorldModel = worldModel;
			IEvent.PostToGameObject( WorldModel, x => x.OnCreateWorldModel() );
		}
	}

	protected override void DestroyWorldModel()
	{
		if ( WorldModel.IsValid() )
			IEvent.PostToGameObject( WorldModel, x => x.OnDestroyWorldModel() );

		WorldModel?.Destroy();
		WorldModel = default;

		if ( Networking.IsHost )
			IsItem = true;
	}
}
