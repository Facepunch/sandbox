using Sandbox;

public class ParticlesUndo : ICanUndo
{
	internal Particles particles { get; set; }

	public ParticlesUndo( Particles particles ) => this.particles = particles;

	public void DoUndo() => particles?.Destroy();

	public bool IsValidUndo() => particles != null;
}
