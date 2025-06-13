using Sandbox.Rendering;

public partial class Toolgun : BaseCarryable
{
	Material screenCopie;
	Texture screenTexture;

	void UpdateViewmodelScreen()
	{
		if ( !ViewModel.IsValid() ) return;

		var modelRenderer = ViewModel.GetComponentInChildren<SkinnedModelRenderer>();
		if ( !modelRenderer.IsValid() ) return;

		// which index holds the screen?
		var oldMaterial = modelRenderer.Model.Materials.Where( x => x.Name.Contains( "toolgun_screen" ) ).FirstOrDefault();
		var index = modelRenderer.Model.Materials.IndexOf( oldMaterial );
		if ( index < 0 ) return;

		screenTexture ??= Texture.CreateRenderTarget().WithSize( 512, 128 ).WithInitialColor( Color.Red ).WithMips().Create();
		screenTexture.Clear( Color.Random );

		screenCopie ??= Material.Load( "weapons/toolgun/toolgun-screen.vmat" ).CreateCopy();
		screenCopie.Attributes.Set( "Emissive", screenTexture );
		modelRenderer.SceneObject.Attributes.Set( "Emissive", screenTexture );

		modelRenderer.Materials.SetOverride( index, screenCopie );

		UpdateViewScreenCommandList( modelRenderer );
	}

	void UpdateViewScreenCommandList( SkinnedModelRenderer renderer )
	{
		if ( CurrentMode is null )
			return;

		var rt = RenderTarget.From( screenTexture );

		var cl = new CommandList();
		renderer.ExecuteBefore = cl;

		cl.SetRenderTarget( rt );
		cl.Clear( Color.Black );

		CurrentMode.DrawScreen( new Rect( 0, screenTexture.Size ), cl.Paint );

		cl.ClearRenderTarget();
		cl.GenerateMipMaps( rt );
	}
}
