/// <summary>
/// Called only on the Player's GameObject for their actions or events
/// </summary>
public interface IPlayerEvent : ISceneEvent<IPlayerEvent>
{
	void OnSpawned() { }

	public struct DamageParams
	{
		public float Damage { get; set; }
		public Guid InstigatorId { get; set; }
		public GameObject Attacker { get; set; }
		public GameObject Weapon { get; set; }
		public TagSet Tags { get; set; }
		public Vector3 Position { get; set; }
		public Vector3 Origin { get; set; }
	}
	void OnDamage( DamageParams args ) { }

	public struct DiedParams
	{
		public Guid InstigatorId { get; set; }
		public GameObject Attacker { get; set; }
	}
	void OnDied( DiedParams args ) { }

	void OnJump() { }
	void OnLand( float distance, Vector3 velocity ) { }
	void OnSuicide() { }
	void OnPickup( BaseCarryable item ) { }
	void OnCameraMove( ref Angles angles ) { }
	void OnCameraSetup( CameraComponent camera ) { }
	void OnCameraPostSetup( CameraComponent camera ) { }
}

/// <summary>
/// Broadcasted to the entire scene on the local player's actions or events
/// </summary>
public interface ILocalPlayerEvent : ISceneEvent<ILocalPlayerEvent>
{
	void OnJump() { }
	void OnLand( float distance, Vector3 velocity ) { }
	void OnPickup( BaseCarryable weapon ) { }
}
