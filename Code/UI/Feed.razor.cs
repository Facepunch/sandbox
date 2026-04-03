using Sandbox.UI;

namespace Sandbox;

partial class Feed : PanelComponent
{
	[Property] public Texture DeathIcon { get; set; }
	[Property] public Texture HeadshotIcon { get; set; }
	[Property] public Texture ExplosionIcon { get; set; }
	[Property] public Texture SuicideIcon { get; set; }
	[Property] public Texture FallIcon { get; set; }
	[Property] public Texture NpcIcon { get; set; }

	protected override void OnUpdate()
	{
		SetClass( "hide", Player.FindLocalPlayer()?.WantsHideHud ?? false );
	}

	[Rpc.Broadcast]
	public void NotifyKill( string victimName, string attackerName, long attackerSteamId, string damageTags, string attackerTags, string victimTags, Texture weaponIcon )
	{
		if ( Application.IsDedicatedServer ) return;
		if ( string.IsNullOrEmpty( victimName ) ) return;

		bool isSuicide = damageTags.Contains( "suicide" );

		Panel panel = new Panel();

		if ( !string.IsNullOrEmpty( attackerName ) && !isSuicide )
		{
			if ( attackerTags.Contains( "npc" ) ) AddIcon( panel, NpcIcon );
			var left = panel.AddChild<Label>();
			left.Text = attackerName;
		}

		Panel icons = panel.AddChild<Panel>( "icons" );
		if ( weaponIcon.IsValid() )
		{
			AddIcon( icons, weaponIcon );
		}
		else if ( damageTags.Contains( DamageTags.Fall ) )
		{
			AddIcon( icons, FallIcon );
		}
		else
		{
			AddIcon( icons, isSuicide ? SuicideIcon : DeathIcon );
		}

		if ( damageTags.Contains( DamageTags.Headshot ) ) AddIcon( icons, HeadshotIcon );
		if ( damageTags.Contains( DamageTags.Explosion ) ) AddIcon( icons, ExplosionIcon );

		if ( victimTags.Contains( "npc" ) ) AddIcon( panel, NpcIcon );
		var right = panel.AddChild<Label>();
		right.Text = victimName;

		bool isMe = attackerSteamId > 0 && attackerSteamId == Connection.Local.SteamId;
		if ( isMe )
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
