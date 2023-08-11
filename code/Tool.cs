using Sandbox;
using Sandbox.Tools;

[Library( "weapon_tool", Title = "Toolgun" )]
partial class Tool : Carriable
{
	[ConVar.ClientData( "tool_current" )]
	public static string UserToolCurrent { get; set; } = "tool_boxgun";

	public AnimatedEntity ViewModelArms { get; set; }

	[Net, Change]
	public BaseTool CurrentTool { get; set; }

	public override void Spawn()
	{
		base.Spawn();

		SetModel( "models/weapons/toolgun.vmdl" );
	}

	public override void CreateViewModel()
	{
		base.CreateViewModel();

		ViewModelEntity = new ViewModel();
		ViewModelEntity.Position = Position;
		ViewModelEntity.Owner = Owner;
		ViewModelEntity.EnableViewmodelRendering = true;
		ViewModelEntity.Model = Model.Load( "models/weapons/v_toolgun.vmdl" );

		ViewModelArms = new AnimatedEntity( "models/first_person/first_person_arms.vmdl" );
		ViewModelArms.SetParent( ViewModelEntity, true );
		ViewModelArms.EnableViewmodelRendering = true;
	}

	public override void Simulate( IClient owner )
	{
		if ( Game.IsServer )
		{
			UpdateCurrentTool( owner );
		}

		CurrentTool?.Simulate();

		if ( Game.IsServer )
		{
			CurrentTool?.UpdatePreviews();
		}
	}

	private void UpdateCurrentTool( IClient owner )
	{
		var toolName = owner.GetClientData<string>( "tool_current", "tool_balloon" );
		if ( toolName == null )
			return;

		// Already the right tool
		if ( CurrentTool != null && CurrentTool.ClassName == toolName )
			return;

		if ( CurrentTool != null )
		{
			CurrentTool?.Deactivate();
			CurrentTool = null;
		}

		CurrentTool = TypeLibrary.Create<BaseTool>( toolName );

		if ( CurrentTool != null )
		{
			CurrentTool.Parent = this;
			CurrentTool.Owner = owner.Pawn as Player;
			CurrentTool.Activate();
		}
	}

	// Note: called clientside only
	private void OnCurrentToolChanged( BaseTool oldTool, BaseTool newTool )
	{
		oldTool?.Deactivate();
		newTool?.Activate();
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

	[Event.Client.Frame]
	public void OnFrame()
	{
		if ( Owner is Player player && player.ActiveChild != this )
			return;

		CurrentTool?.OnFrame();
	}

	public override void SimulateAnimator( CitizenAnimationHelper anim )
	{
		anim.HoldType = CitizenAnimationHelper.HoldTypes.Pistol;
		anim.Handedness = CitizenAnimationHelper.Hand.Right;
		anim.AimBodyWeight = 1.0f;
	}
}

namespace Sandbox.Tools
{
	public partial class BaseTool : BaseNetworkable
	{
		[Net]
		public Tool Parent { get; set; }

		[Net]
		public Player Owner { get; set; }

		protected virtual float MaxTraceDistance => 10000.0f;

		// Set this to override the [Library]'s class default
		public string Description { get; set; } = null;

		public virtual void Activate()
		{
			if ( Game.IsServer )
			{
				CreatePreviews();
				CurrentTool.CreateToolPanel();
			}
		}

		public virtual void CreateToolPanel()
		{

		}

		public virtual void Deactivate()
		{
			DeletePreviews();
			SpawnMenu.Instance?.ToolPanel?.DeleteChildren( true );
		}

		public virtual void Simulate()
		{

		}

		public virtual void OnFrame()
		{
			UpdatePreviews();
		}

		public virtual void CreateHitEffects( Vector3 pos, Vector3 normal = new Vector3(), bool continuous = false )
		{
			Parent?.CreateHitEffects( pos, normal, continuous );
		}

		public virtual TraceResult DoTrace()
		{
			var startPos = Owner.EyePosition;
			var dir = Owner.EyeRotation.Forward;

			return Trace.Ray( startPos, startPos + (dir * MaxTraceDistance) )
				.WithAnyTags( "solid", "nocollide" )
				.Ignore( Owner )
				.Run();
		}

		protected string GetConvarValue( string name, string defaultValue = null )
		{
			return Game.IsServer
				? Owner.Client.GetClientData<string>( name, defaultValue )
				: ConsoleSystem.GetValue( name, default );
		}
	}
}
