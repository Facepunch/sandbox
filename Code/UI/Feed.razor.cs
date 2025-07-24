using Sandbox.UI;

namespace Sandbox;

partial class Feed
{
	[Property] public Texture DeathIcon { get; set; }
	[Property] public Texture HeadshotIcon { get; set; }
	[Property] public Texture ExplosionIcon { get; set; }
	[Property] public Texture SuicideIcon { get; set; }
	[Property] public Texture FallIcon { get; set; }
	[Property] public Texture MultiKillIcon { get; set; }

	private string KillsToText( int kills )
	{
		return kills switch
		{
			2 => "double kill!",
			3 => "triple kill!",
			4 => "monster kill!!",
			5 => "killtacular!!",
			6 => "killimanjaro!!!",
			//
			_ => $"multi kill ({kills})!!!"
		};
	}

	[Rpc.Broadcast]
	public void NotifyKill( PlayerData attacker, int kills )
	{
		if ( Application.IsDedicatedServer ) return;
		if ( !attacker.IsValid() ) return;

		if ( attacker.IsMe )
		{
			var x = Sound.Play( "kill_sound" );
			x.Pitch = 1f + (1f / 12f * (kills - 2));
		}

		if ( kills < 2 ) return;

		var panel = new Panel();

		var icons = panel.AddChild<Panel>( "icons" );

		AddIcon( icons, MultiKillIcon );

		var left = panel.AddChild<Label>();
		left.Text = attacker.DisplayName;

		var right = panel.AddChild<Label>();
		right.Text = KillsToText( kills );

		if ( attacker.IsValid() && attacker.IsMe )
			panel.AddClass( "is-me" );
		panel.FlashClass( "important", 1f );

		Panel?.AddChild( panel );
		Invoke( 7, () => panel.Delete() );
	}

	protected override void OnUpdate()
	{
		SetClass( "hide", Player.FindLocalPlayer()?.WantsHideHud ?? false );
	}

	[Rpc.Broadcast]
	public void NotifyDeath( PlayerData victim, PlayerData attacker, Texture weaponIcon, TagSet tags )
	{
		if ( Application.IsDedicatedServer ) return;

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
