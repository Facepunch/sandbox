namespace Sandbox;

public class ClientInput : Component
{
	/// <summary>
	/// Component we're going to call the method on
	/// </summary>
	[Property]
	public Component TargetComponent { get; set; }

	/// <summary>
	/// Method we're going to call
	/// </summary>
	[Property]
	public string TargetMethod { get; set; }

	/// <summary>
	/// The button name to listen for
	/// </summary>
	[Property]
	public string InputName { get; set; }

	protected override void OnFixedUpdate()
	{
		// TODO - only called from chair or something?
		OnInput( Connection.Local );
	}

	public virtual void OnInput( Connection c )
	{
		// TODO - lets call the method every tick?
		if ( !c.Pressed( InputName ) ) return;

		// TODO - call with a special context? Allow the target method to do special shit?
		var method = TypeLibrary.GetType( TargetComponent.GetType() ).GetMethod( TargetMethod );
		method.Invoke( TargetComponent );
	}

}
