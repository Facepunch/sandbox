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
		var rt = RenderTarget.From( screenTexture );

		var cl = new CommandList();
		renderer.ExecuteBefore = cl;

		cl.SetRenderTarget( rt );
		cl.Clear( Color.Black );

		{
			var text = new TextRendering.Scope( "weld ⚠️", Color.White, 100 );
			text.LineHeight = 0.75f;
			text.FontName = "Poppins";
			text.TextColor = Color.Lerp( Color.Yellow, Color.Orange, 0.5f + Random.Shared.Float( -0.4f, 0.4f ) );
			text.TextColor = Color.Lerp( text.TextColor, Color.White, 0.1f );
			text.FontWeight = 700;

			cl.Paint.DrawText( text, new Rect( 0, screenTexture.Size ), TextFlag.CenterBottom );
		}

		cl.ClearRenderTarget();
		cl.GenerateMipMaps( rt );
	}
}
