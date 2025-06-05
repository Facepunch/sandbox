public partial class BaseWeapon
{
	[Rpc.Broadcast]
	protected virtual void StartAttack()
	{
		var ev = new IWeaponEvent.AttackEvent( ViewModel.IsValid() );
		IWeaponEvent.PostToGameObject( GameObject.Root, x => x.OnAttackStart( ev ) );
	}

	[Rpc.Broadcast]
	protected virtual void StopAttack()
	{
		IWeaponEvent.PostToGameObject( GameObject.Root, x => x.OnAttackStop() );
	}

	/// <summary>
	/// Events related to weapons
	/// </summary>
	public interface IWeaponEvent : ISceneEvent<IWeaponEvent>
	{
		/// <summary>
		/// Data structure that holds simple information when we shoot
		/// </summary>
		public record struct AttackEvent( bool isFirstPerson );

		/// <summary>
		/// Called when we start firing (if we're firing automatic, it should only call when we press the attack key)
		/// </summary>
		/// <param name="e"></param>
		void OnAttackStart( AttackEvent e ) { }

		/// <summary>
		/// Called when we stop firing (release the attack key)
		/// </summary>
		void OnAttackStop() { }

		/// <summary>
		/// Called for every attack (NOT including each pellet for something like a shotgun - so once per shot)
		/// </summary>
		/// <param name="e"></param>
		void OnAttack( AttackEvent e ) { }

		/// <summary>
		/// Called for every ranged attack to create effects for the shot (eg tracers)
		/// </summary>
		void CreateRangedEffects( BaseWeapon weapon, Vector3 hitpoint, Vector3? origin = null ) { }

		/// <summary>
		/// Called when we start reloading a weapon
		/// </summary>
		void OnReloadStart() { }

		/// <summary>
		/// Called when we successfully finish reloading a weapon
		/// </summary>
		void OnReloadFinish() { }

		/// <summary>
		/// Called when we reload incrementally
		/// </summary>
		void OnIncrementalReload() { }
	}
}
