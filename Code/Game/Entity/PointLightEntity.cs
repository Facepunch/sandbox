/// <summary>
/// A player-controlled point light.
/// </summary>
[Title( "#entity.name.pointlightentity" )]
public sealed class PointLightEntity : Component, IPlayerControllable
{
	/// <summary>
	/// Whether the light is switched on.
	/// </summary>
	[Property, Title( "#entity.property.on" ), Description( "#entity.description.on" ), ClientEditable, Group( "#entity.group.light" ), SignalInput( Default = true )]
	public bool On { get; set { field = value; UpdateLight(); } } = true;

	/// <summary>
	/// Whether the light casts shadows.
	/// </summary>
	[Property, Title( "#entity.property.shadows" ), Description( "#entity.description.shadows" ), ClientEditable, Group( "#entity.group.light" )]
	public bool Shadows { get; set { field = value; UpdateLight(); } }

	/// <summary>
	/// The color of the light.
	/// </summary>
	[Property, Title( "#entity.property.color" ), Description( "#entity.description.color" ), Range( 0, 1 ), ClientEditable, Group( "#entity.group.light" )]
	public Color Color { get; set { field = value; UpdateLight(); } }

	/// <summary>
	/// The brightness of the light.
	/// </summary>
	[Property, Title( "#entity.property.brightness" ), Description( "#entity.description.brightness" ), Range( 0, 50 ), ClientEditable, Group( "#entity.group.light" )]
	public float Brightness { get; set { field = value; UpdateLight(); } }

	/// <summary>
	/// The radius affected by the light.
	/// </summary>
	[Property, Title( "#entity.property.radius" ), Description( "#entity.description.radius" ), Range( 0, 1000 ), ClientEditable, Group( "#entity.group.light" )]
	public float Radius { get; set { field = value; UpdateLight(); } }

	/// <summary>
	/// How quickly the light fades with distance.
	/// </summary>
	[Property, Title( "#entity.property.attenuation" ), Description( "#entity.description.attenuation" ), Range( 0, 16 ), ClientEditable, Group( "#entity.group.light" )]
	public float Attenuation { get; set { field = value; UpdateLight(); } } = 2.4f;


	/// <summary>
	/// The input that switches the light on.
	/// </summary>
	[Property, Title( "#entity.property.turnon" ), Description( "#entity.description.turnon" ), ClientEditable, Group( "#entity.group.state" )]
	public ClientInput TurnOn { get; set; }

	/// <summary>
	/// The input that switches the light off.
	/// </summary>
	[Property, Title( "#entity.property.turnoff" ), Description( "#entity.description.turnoff" ), ClientEditable, Group( "#entity.group.state" )]
	public ClientInput TurnOff { get; set; }

	/// <summary>
	/// The input that toggles the light.
	/// </summary>
	[Property, Title( "#entity.property.toggle" ), Description( "#entity.description.toggle" ), ClientEditable, Group( "#entity.group.state" )]
	public ClientInput Toggle { get; set; }

	/// <summary>
	/// The object shown while the light is on.
	/// </summary>
	[Property]
	public GameObject OnGameObject { get; set; }

	/// <summary>
	/// The object shown while the light is off.
	/// </summary>
	[Property]
	public GameObject OffGameObject { get; set; }

	/// <summary>
	/// Toggles the light when its signal input is triggered.
	/// </summary>
	[SignalInput( Id = nameof( Toggle ) ), Title( "#entity.property.toggle" )]
	public void ToggleSignal() => On = !On;

	/// <summary>
	/// Switches the light on when its signal input is triggered.
	/// </summary>
	[SignalInput( Id = nameof( TurnOn ) ), Title( "#entity.property.turnon" )]
	public void TurnOnSignal() => On = true;

	/// <summary>
	/// Switches the light off when its signal input is triggered.
	/// </summary>
	[SignalInput( Id = nameof( TurnOff ) ), Title( "#entity.property.turnoff" )]
	public void TurnOffSignal() => On = false;

	/// <summary>
	/// Applies the owning player's light controls.
	/// </summary>
	public void OnControl()
	{
		if ( Toggle.Pressed() ) On = !On;
		if ( TurnOn.Pressed() ) On = true;
		if ( TurnOff.Pressed() ) On = false;
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
