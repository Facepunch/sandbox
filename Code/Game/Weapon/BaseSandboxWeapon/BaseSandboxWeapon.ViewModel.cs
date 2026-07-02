public partial class BaseSandboxWeapon
{
	// ViewModelPrefab is inherited from the engine BaseWeapon (same serialized name). We override the
	// creation so we can call the game ViewModel component's Deploy().

	protected override void CreateViewModel()
	{
		if ( ViewModel.IsValid() )
			return;

		DestroyViewModel();

		if ( ViewModelPrefab is null )
			return;

		var player = Owner;
		if ( player is null || player.Controller is null || player.Controller.Renderer is null || !player.IsLocalPlayer ) return;

		ViewModel = ViewModelPrefab.Clone( new CloneConfig { Parent = GameObject, StartEnabled = false, Transform = global::Transform.Zero } );
		ViewModel.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked | GameObjectFlags.Absolute;
		ViewModel.Enabled = true;
		ViewModel.Tags.Add( "firstperson", "viewmodel" );

		var vm = ViewModel.GetComponent<ViewModel>();

		if ( vm.IsValid() )
			vm.OnDeploy();
	}

	protected override void DestroyViewModel()
	{
		if ( !ViewModel.IsValid() )
			return;

		ViewModel?.Destroy();
		ViewModel = default;
	}
}
