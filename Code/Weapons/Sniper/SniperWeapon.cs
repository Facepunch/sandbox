using Sandbox.Rendering;

public class SniperWeapon : BaseBulletWeapon
{
	[Property] public float PrimaryFireRate { get; set; } = 1.2f;
	[Property] public float ScopedFov { get; set; } = 20f;
	[Property] public float ScopeSensitivity { get; set; } = 0.3f;

	private bool _isScoped;
	private float _mouseDelta;
	private SniperScopeEffect _scopeEffect;
	private bool _hasFired;

	public bool IsScoped => _isScoped;

	protected override float GetPrimaryFireRate() => PrimaryFireRate;

	protected override bool WantsPrimaryAttack()
	{
		return Input.Pressed( "attack1" );
	}

	protected override bool WantsSecondaryAttack()
	{
		return Input.Pressed( "attack2" );
	}

	public override bool CanSecondaryAttack()
	{
		return true;
	}

	public override void PrimaryAttack()
	{
		ShootBullet( PrimaryFireRate );
		_hasFired = true;
	}

	public override void SecondaryAttack()
	{
		SetScoped( !_isScoped );
	}

	private void SetScoped( bool scoped )
	{
		_isScoped = scoped;

		if ( _isScoped )
		{
			_scopeEffect = Scene.Camera.Components.GetOrCreate<SniperScopeEffect>();
			_scopeEffect.Flags |= ComponentFlags.NotNetworked;
		}
		else
		{
			_scopeEffect?.Destroy();
			_scopeEffect = default;
		}
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		if ( _isScoped )
			SetScoped( false );
	}

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		if ( _hasFired && Input.Released( "attack1" ) )
		{
			_hasFired = false;
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

		if ( _isScoped )
		{
			camera.FieldOfView = ScopedFov;
		}
	}

	public override void OnCameraMove( Player player, ref Angles angles )
	{
		_mouseDelta = new Vector2( angles.yaw, angles.pitch ).Length;

		if ( _isScoped )
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
		var color = !HasAmmo() || IsReloading() || TimeUntilNextShotAllowed > 0 ? CrosshairNoShoot : CrosshairCanShoot;

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
