using Sandbox.UI;

namespace Sandbox;

partial class Feed : PanelComponent
{
	[Property] public Texture DeathIcon { get; set; }
	[Property] public Texture HeadshotIcon { get; set; }
	[Property] public Texture ExplosionIcon { get; set; }
	[Property] public Texture SuicideIcon { get; set; }
	[Property] public Texture FallIcon { get; set; }

	protected override void OnUpdate()
	{
		SetClass( "hide", Player.FindLocalPlayer()?.WantsHideHud ?? false );
	}

	[Rpc.Broadcast]
	public void NotifyDeath( PlayerData victim, PlayerData attacker, Texture weaponIcon, TagSet tags )
	{
		if ( Application.IsDedicatedServer ) return;
		if ( !victim.IsValid() ) return;

		Panel panel = new Panel();

		bool isSuicide = victim == attacker;
		if ( attacker.IsValid() && !isSuicide )
		{
			var left = panel.AddChild<Label>();
			left.Text = attacker.DisplayName;
		}

		Panel icons = panel.AddChild<Panel>( "icons" );
		if ( weaponIcon.IsValid() )
		{
			AddIcon( icons, weaponIcon );
		}
		else if ( tags.Contains( DamageTags.Fall ) )
		{
			AddIcon( icons, FallIcon );
		}
		else
		{
			AddIcon( icons, isSuicide ? SuicideIcon : DeathIcon );
		}

		if ( tags.Contains( DamageTags.Headshot ) ) AddIcon( icons, HeadshotIcon );
		if ( tags.Contains( DamageTags.Explosion ) ) AddIcon( icons, ExplosionIcon );

		var right = panel.AddChild<Label>();
		right.Text = victim.DisplayName;

		if ( attacker.IsValid() && attacker.IsMe )
			panel.AddClass( "is-me" );

		Panel?.AddChild( panel );
		Invoke( 7, () => panel.Delete() );
	}

	private Panel AddIcon( in Panel panel, Texture icon )
	{
		if ( !icon.IsValid() )
		{
			Log.Warning( "Couldn't create kill feed icon" );
			return null;
		}

		if ( icon.Width < 1 || icon.Height < 1 )
		{
			Log.Warning( "Tried to add an icon that is zero-sized" );
			return null;
		}

		var iconPanel = panel.AddChild<Panel>( "icon" );
		iconPanel.Style.SetBackgroundImage( icon );
		iconPanel.Style.AspectRatio = icon.Width / icon.Height;

		return iconPanel;
	}
}
