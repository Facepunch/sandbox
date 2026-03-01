using Sandbox.Rendering;

public partial class ScreenWeapon : BaseCarryable
{
	private Material _screenMaterialCopy;
	private Texture _screenTexture;
	private float _coilSpin;
	private float _joystickX;
	private float _joystickY;

	/// <summary>
	/// Override to match a different model's screen material.
	/// </summary>
	protected virtual string ScreenMaterialName => "toolgun_screen";

	/// <summary>
	/// Override to use a different screen material
	/// </summary>
	protected virtual string ScreenMaterialPath => "weapons/toolgun/toolgun-screen.vmat";

	protected virtual Vector2Int ScreenTextureSize => new( 512, 128 );

	/// <summary>
	/// Add energy to the coil spin (e.g. on fire).
	/// </summary>
	public void SpinCoil()
	{
		_coilSpin += 10;
	}

	/// <summary>
	/// Smoothly decays the coil spin and applies it to the viewmodel's "coil" object.
	/// </summary>
	protected void ApplyCoilSpin()
	{
		_coilSpin = _coilSpin.LerpTo( 0, Time.Delta * 1 );

		if ( !ViewModel.IsValid() ) return;

		var coil = ViewModel.GetAllObjects( true ).FirstOrDefault( x => x.Name == "coil" );
		if ( coil.IsValid() )
		{
			coil.WorldRotation *= Rotation.From( 0, 0, _coilSpin );
		}
	}

	/// <summary>
	/// Updates joystick_x and joystick_y on the viewmodel based on a per-frame look delta.
	/// </summary>
	protected void UpdateJoystick( Angles lookDelta )
	{
		_joystickX = _joystickX.LerpTo( lookDelta.yaw.Clamp( -1f, 1f ), Time.Delta * 10f );
		_joystickY = _joystickY.LerpTo( lookDelta.pitch.Clamp( -1f, 1f ), Time.Delta * 10f );

		WeaponModel?.Renderer?.Set( "joystick_x", _joystickX );
		WeaponModel?.Renderer?.Set( "joystick_y", _joystickY );
	}

	protected void SetIsUsingJoystick( bool isUsing )
	{
		WeaponModel?.Renderer?.Set( "b_joystick", isUsing );
	}

	/// <summary>
	/// Updates the viewmodel screen render target and redraws it.
	/// </summary>
	protected void UpdateViewmodelScreen()
	{
		if ( !ViewModel.IsValid() ) return;

		var modelRenderer = ViewModel.GetComponentInChildren<SkinnedModelRenderer>();
		if ( !modelRenderer.IsValid() ) return;

		var oldMaterial = modelRenderer.Model.Materials.FirstOrDefault( x => x.Name.Contains( ScreenMaterialName ) );
		var index = modelRenderer.Model.Materials.IndexOf( oldMaterial );
		if ( index < 0 ) return;

		_screenTexture ??= Texture.CreateRenderTarget().WithSize( ScreenTextureSize.x, ScreenTextureSize.y ).WithInitialColor( Color.Red ).WithMips()
			.Create();
		_screenTexture.Clear( Color.Random );

		_screenMaterialCopy ??= Material.Load( ScreenMaterialPath ).CreateCopy();
		_screenMaterialCopy.Attributes.Set( "Emissive", _screenTexture );
		modelRenderer.SceneObject.Attributes.Set( "Emissive", _screenTexture );

		modelRenderer.Materials.SetOverride( index, _screenMaterialCopy );

		UpdateViewScreenCommandList( modelRenderer );
	}

	private void UpdateViewScreenCommandList( SkinnedModelRenderer renderer )
	{
		var rt = RenderTarget.From( _screenTexture );

		var cl = new CommandList();
		renderer.ExecuteBefore = cl;

		cl.SetRenderTarget( rt );
		cl.Clear( Color.Black );

		DrawScreenContent( new Rect( 0, _screenTexture.Size ), cl.Paint );

		cl.ClearRenderTarget();
		cl.GenerateMipMaps( rt );
	}

	/// <summary>
	/// Override this to draw custom content onto the viewmodel screen.
	/// </summary>
	protected virtual void DrawScreenContent( Rect rect, HudPainter paint )
	{
	}
}
