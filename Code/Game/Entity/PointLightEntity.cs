

public class PointLightEntity : Component, IPlayerControllable
{
	[Property, ClientEditable, Group( "Light" )]
	public bool On { get; set { field = value; UpdateLight(); } } = true;

	[Property, ClientEditable, Group( "Light" )]
	public bool Shadows { get; set { field = value; UpdateLight(); } }

	[Property, Range( 0, 1 ), ClientEditable, Group( "Light" )]
	public Color Color { get; set { field = value; UpdateLight(); } }

	[Property, Range( 0, 50 ), ClientEditable, Group( "Light" )]
	public float Brightness { get; set { field = value; UpdateLight(); } }

	[Property, Range( 0, 1000 ), ClientEditable, Group( "Light" )]
	public float Radius { get; set { field = value; UpdateLight(); } }

	[Property, Range( 0, 16 ), ClientEditable, Group( "Light" )]
	public float Attenuation { get; set { field = value; UpdateLight(); } } = 2.4f;


	[Property, Sync, ClientEditable, Group( "State" )]
	public ClientInput TurnOn { get; set; }

	[Property, Sync, ClientEditable, Group( "State" )]
	public ClientInput TurnOff { get; set; }

	[Property, Sync, ClientEditable, Group( "State" )]
	public ClientInput Toggle { get; set; }

	[Property]
	public GameObject OnGameObject { get; set; }

	[Property]
	public GameObject OffGameObject { get; set; }

	void IPlayerControllable.OnControl()
	{

		if ( Toggle.Pressed() )
		{
			On = !On;
		}

		if ( TurnOn.Pressed() )
		{
			On = true;
		}

		if ( TurnOff.Pressed() )
		{
			On = false;
		}
	}

	void IPlayerControllable.OnEndControl()
	{

	}

	void IPlayerControllable.OnStartControl()
	{

	}

	void UpdateLight()
	{
		OnGameObject?.Enabled = On;
		OffGameObject?.Enabled = !On;

		if ( GetComponentInChildren<PointLight>( true ) is not PointLight light )
			return;

		light.Enabled = On;

		var color = Color;
		color.r *= Brightness;
		color.g *= Brightness;
		color.b *= Brightness;

		light.Shadows = Shadows;
		light.LightColor = color;
		light.Radius = Radius;
		light.Attenuation = Attenuation;

		Network.Refresh();
	}
}
