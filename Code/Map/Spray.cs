public sealed class Spray : Component
{
	[Property]
	GameObject SpraySmoke { get; set; }

	protected override void OnStart()
	{
		if ( IsProxy ) return;

		CreateMaterial();
	}

	[Rpc.Broadcast]
	private void CreateMaterial()
	{
		if ( Application.IsDedicatedServer ) return;
		if ( Network.Owner is null ) return;

		var decalrender = Components.Get<Decal>();
		var texture = Texture.Load( $"avatarbig:{Network.Owner.SteamId}" );
		decalrender.ColorTexture = texture;

		if ( !SpraySmoke.IsValid() ) return;

		var smoke = SpraySmoke.Clone( WorldPosition, Rotation.Identity );

		// This seems stupid, wouldn't it create a smoke for each person on the server and then try to network spawn it every time?
		// smoke.NetworkSpawn();

		var pixel = texture.Height / 2 * texture.Width / 2;
		var pix = texture.GetPixels( pixel );
		pix.FirstOrDefault().ToColor();

		var pe = smoke.GetComponent<ParticleEffect>();

		if ( !pe.IsValid() )
			return;

		pe.Tint = pix.FirstOrDefault().ToColor();
	}
}
