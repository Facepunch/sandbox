using Sandbox;
using Sandbox.Tools;

[Library( "weapon_tool", Title = "Toolgun" )]
partial class Tool : Carriable
{
	[ConVar.ClientData( "tool_current" )]
	public static string UserToolCurrent { get; set; } = "tool_boxgun";

	public AnimatedEntity ViewModelArms { get; set; }

	[Net]
	public BaseTool CurrentTool { get; set; }

	public override void Spawn()
	{
		base.Spawn();

		SetModel( "weapons/rust_pistol/rust_pistol.vmdl" );
	}

	public override void CreateViewModel()
	{
		base.CreateViewModel();
		
		ViewModelEntity = new ViewModel();
		ViewModelEntity.Position = Position;
		ViewModelEntity.Owner = Owner;
		ViewModelEntity.EnableViewmodelRendering = true;
		ViewModelEntity.Model = Cloud.Model( "https://asset.party/facepunch/v_toolgun" );

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

		public virtual void Activate()
		{
			if ( Game.IsServer )
			{
				CreatePreviews();
			}
		}

		public virtual void Deactivate()
		{
			DeletePreviews();
		}

		public virtual void Simulate()
		{

		}

		public virtual void OnFrame()
		{
			UpdatePreviews();
		}

		public virtual void CreateHitEffects( Vector3 pos )
		{
			Parent?.CreateHitEffects( pos );
		}

		public virtual TraceResult DoTrace()
		{
			var startPos = Owner.EyePosition;
			var dir = Owner.EyeRotation.Forward;

			return Trace.Ray( startPos, startPos + ( dir * MaxTraceDistance ) )
				.WithAnyTags( "solid", "nocollide" )
				.Ignore( Owner )
				.Run();
		}
	}
}
