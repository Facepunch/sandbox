using Sandbox.Rendering;

public sealed class SniperWeapon : BaseBulletWeapon
{
	[Property] public float ScopedFov { get; set; } = 20f;
	[Property] public float ScopeSensitivity { get; set; } = 0.3f;
	[Property] public SoundEvent BoltPullSound { get; set; }

	private bool _isScoped;
	private float _mouseDelta;
	private SniperScopeEffect _scopeEffect;
	private bool _hasFired;
	private TimeUntil _timeUntilHideViewModel;
	private bool _viewModelHidden;

	public bool IsScoped => _isScoped;

	protected override bool WantsPrimaryAttack()
	{
		return Input.Pressed( "attack1" );
	}

	protected override bool WantsSecondaryAttack()
	{
		return false;
	}

	public override bool CanSecondaryAttack()
	{
		return false;
	}

	public override void PrimaryAttack()
	{
		ShootBullet();
		_hasFired = true;
	}

	private void SetScoped( bool scoped )
	{
		_isScoped = scoped;

		// Trigger ironsights animation
		ViewModel?.RunEvent<ViewModel>( x =>
		{
			x.Renderer?.Set( "ironsights", _isScoped ? 1 : 0 );
		} );

		if ( _isScoped )
		{
			// Delay hiding the viewmodel until the ADS animation finishes
			_timeUntilHideViewModel = 0.2f;
		}
		else
		{
			ShowViewModel();

			_scopeEffect?.Destroy();
			_scopeEffect = default;
		}
	}

	private void HideViewModel()
	{
		if ( _viewModelHidden ) return;
		_viewModelHidden = true;

		if ( ViewModel.IsValid() )
			Scene.Camera.RenderExcludeTags.Add( "sniper_scoped" );

		if ( ViewModel.IsValid() )
			ViewModel.Tags.Add( "sniper_scoped" );
	}

	private void ShowViewModel()
	{
		if ( !_viewModelHidden ) return;
		_viewModelHidden = false;

		Scene.Camera.RenderExcludeTags.Remove( "sniper_scoped" );

		if ( ViewModel.IsValid() )
			ViewModel.Tags.Remove( "sniper_scoped" );
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		if ( _isScoped )
			SetScoped( false );

		ShowViewModel();
	}

	protected override void OnControl()
	{
		base.OnControl();

		// Hold right mouse to scope
		var wantsScope = Input.Down( "attack2" );
		if ( wantsScope != _isScoped )
		{
			SetScoped( wantsScope );
		}

		// Hide viewmodel once ADS animation has finished, then enable scope overlay
		if ( _isScoped && !_viewModelHidden && _timeUntilHideViewModel )
		{
			HideViewModel();

			_scopeEffect = Scene.Camera.Components.GetOrCreate<SniperScopeEffect>();
			_scopeEffect.Flags |= ComponentFlags.NotNetworked;
		}

		if ( _hasFired && Input.Released( "attack1" ) )
		{
			_hasFired = false;

			if ( BoltPullSound is not null )
				Sound.Play( BoltPullSound, WorldPosition );

			ViewModel?.RunEvent<ViewModel>( x =>
			{
				x.Renderer?.Set( "speed_reload", 1 );
				x.Renderer?.Set( "b_reload_bolt", true );
			} );
		}
	}

	public override void OnCameraSetup( Player player, CameraComponent camera )
	{
		if ( !player.Network.IsOwner || !Network.IsOwner ) return;

		if ( _isScoped && _viewModelHidden )
		{
			camera.FieldOfView = ScopedFov;
		}
	}

	public override void OnCameraMove( Player player, ref Angles angles )
	{
		_mouseDelta = new Vector2( angles.yaw, angles.pitch ).Length;

		if ( _isScoped && _viewModelHidden )
		{
			angles *= ScopeSensitivity;
		}
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		if ( _isScoped )
		{
			DrawScopeOverlay( painter, crosshair );
			return;
		}

		DrawCrosshair( painter, crosshair );
	}

	public override void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var color = !HasAmmo() || IsReloading || NextPrimaryFire > 0 ? CrosshairNoShoot : CrosshairCanShoot;

		hud.SetBlendMode( BlendMode.Normal );
		hud.DrawCircle( center, 5, Color.Black );
		hud.DrawCircle( center, 3, color );
	}

	private void DrawScopeOverlay( HudPainter hud, Vector2 center )
	{
		if ( !_scopeEffect.IsValid() )
			return;

		var mouseBlur = _mouseDelta * 0.25f;
		var velocityBlur = HasOwner ? (Owner.Controller.Velocity.Length / 300f).Clamp( 0f, 1f ) : 0f;

		_scopeEffect.BlurInput = MathF.Min( mouseBlur + velocityBlur, 1f );
	}
}
