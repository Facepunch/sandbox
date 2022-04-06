using Sandbox;

[Library( "ent_lamp", Title = "Lamp", Spawnable = true )]
public partial class LampEntity : SpotLightEntity, IUse
{
	public override void Spawn()
	{
		base.Spawn();

		SetModel( "models/torch/torch.vmdl" );
		SetupPhysicsFromModel( PhysicsMotionType.Dynamic, false );
	}

	public bool IsUsable( Entity user )
	{
		return true;
	}

	public bool OnUse( Entity user )
	{
		Enabled = !Enabled;

		PlaySound( Enabled ? "flashlight-on" : "flashlight-off" );

		return false;
	}

	public void Remove()
	{
		Delete();
	}
}
