using Sandbox;
using Sandbox.Tools;

[Library( "weapon_tool", Title = "Toolgun" )]
partial class Tool : Carriable
{
	[ConVar.ClientData( "tool_current" )]
	public static string UserToolCurrent { get; set; } = "tool_boxgun";

	public override string ViewModelPath => "weapons/rust_pistol/v_rust_pistol.vmdl";

	[Net, Predicted]
	public BaseTool CurrentTool { get; set; }

	public override void Spawn()
	{
		base.Spawn();

		SetModel( "weapons/rust_pistol/rust_pistol.vmdl" );
	}

	public override void Simulate( Client owner )
	{
		UpdateCurrentTool( owner );

		CurrentTool?.Simulate();
	}

	private void UpdateCurrentTool( Client owner )
	{
		var toolName = owner.GetClientData<string>( "tool_current", "tool_boxgun" );
		if ( toolName is null )
			return;

		// Already the right tool
		if ( CurrentTool is not null && CurrentTool.Parent == this && CurrentTool.Owner == owner.Pawn && CurrentTool.ClassInfo.IsNamed( toolName ) )
			return;

		if ( CurrentTool is not null )
		{
			CurrentTool?.Deactivate();
			CurrentTool = null;
		}

		CurrentTool = Library.Create<BaseTool>( toolName, false );

		if ( CurrentTool is not null )
		{
			CurrentTool.Parent = this;
			CurrentTool.Owner = owner.Pawn as Player;
			CurrentTool.Activate();
		}
	}

	public override void ActiveStart( Entity ent )
	{
		base.ActiveStart( ent );

		CurrentTool?.Activate();
	}

	public override void ActiveEnd( Entity ent, bool dropped )
	{
		base.ActiveEnd( ent, dropped );

		CurrentTool?.Deactivate();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		CurrentTool?.Deactivate();
		CurrentTool = null;
	}

	public override void OnCarryDrop( Entity dropper )
	{
	}

	[Event.Frame]
	public void OnFrame()
	{
		if ( Owner is Player player && player.ActiveChild != this )
			return;

		CurrentTool?.OnFrame();
	}

	public override void SimulateAnimator( PawnAnimator anim )
	{
		anim.SetAnimParameter( "holdtype", 1 );
		anim.SetAnimParameter( "aim_body_weight", 1.0f );
		anim.SetAnimParameter( "holdtype_handedness", 1 );
	}
}
