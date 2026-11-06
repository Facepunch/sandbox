namespace Sandbox.UI;

/// <summary>
/// Edits a rope material setting as a row of recommended materials, each previewed as a rendered rope, with a
/// button that opens the full material picker (workshop included). The value is a local path or a workshop
/// package ident.
/// </summary>
public sealed class RopeMaterialsAttribute : System.Attribute
{
	/// <summary>
	/// The recommended materials, in the order they're shown.
	/// </summary>
	public string[] Paths { get; }

	/// <summary>
	/// Tooltips for <see cref="Paths"/>, by index. Missing ones use the file name.
	/// </summary>
	public string[] Titles { get; set; }

	/// <summary>
	/// Which of <see cref="Paths"/> are drawn as glowing beams, so their previews are drawn the same way.
	/// </summary>
	public string[] Glow { get; set; }

	/// <summary>
	/// A <see cref="Color"/> property on the same object to tint the previews with, so they match what'll be placed.
	/// </summary>
	public string TintProperty { get; set; }

	public RopeMaterialsAttribute( params string[] paths )
	{
		Paths = paths ?? [];
	}
}
