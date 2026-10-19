using Sandbox.Movement;

public sealed partial class PlayerInventory
{
	/// <summary>
	/// True while the player sits in a seat with their weapons put away. While seated, player cannot switch weapons.
	/// You can use <see cref="PlayerSeatHolsterEvent"/> to cancel holstering and keep weapons equipped while seated.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public bool IsSeatHolstered { get; private set; }

	private ISitTarget _seat;
	private BaseSandboxWeapon _weaponBeforeSeat;

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( !Networking.IsHost ) return;

		var seat = GetComponentInParent<ISitTarget>( false );
		if ( seat == _seat ) return;
		_seat = seat;

		if ( IsSeatHolstered )
		{
			var weapon = _weaponBeforeSeat;
			_weaponBeforeSeat = null;
			IsSeatHolstered = false;

			if ( weapon.IsValid() && weapon.Inventory == this )
				Switch( weapon );
		}

		if ( seat is not null )
			HolsterForSeat( seat );
	}

	private void HolsterForSeat( ISitTarget seat )
	{
		var holsterEvent = new PlayerSeatHolsterEvent { Player = Player, Seat = (seat as Component)?.GameObject, Weapon = ActiveWeapon };
		Local.IPlayerEvents.PostToGameObject( Player.GameObject, e => e.OnSeatHolster( holsterEvent ) );
		Global.IPlayerEvents.Post( e => e.OnPlayerSeatHolster( holsterEvent ) );

		if ( holsterEvent.Cancelled )
			return;

		_weaponBeforeSeat = ActiveWeapon;
		IsSeatHolstered = true;
		ForceHolster();
	}
}
