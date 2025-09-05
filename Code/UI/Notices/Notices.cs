namespace Sandbox.UI;

public class Notices : PanelComponent
{
	protected override void OnEnabled()
	{
		base.OnEnabled();
	}

	public static Notices Current => Game.ActiveScene.Get<Notices>();

	[Rpc.Broadcast]
	public static void AddNotice( string text, float seconds = 5 )
	{
		var current = Current;
		if ( current == null || current.Panel == null ) return;

		var notice = new NoticePanel();

		notice.AddChild( new Label() { Text = text, Classes = "text" } );
		notice.TimeUntilDie = seconds;

		current.Panel.AddChild( notice );
	}

	[Rpc.Broadcast]
	public static void AddNotice( string icon, Color iconColor, string text, float seconds = 5 )
	{
		var current = Current;
		if ( current == null || current.Panel == null ) return;

		var notice = new NoticePanel();

		var iconPanel = new Label() { Text = icon, Classes = "icon" };
		iconPanel.Style.FontColor = iconColor;

		notice.AddChild( iconPanel );
		notice.AddChild( new Label() { Text = text, Classes = "text" } );
		notice.TimeUntilDie = seconds;

		current.Panel.AddChild( notice );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		var innerBox = Panel.Box.RectInner;
		float y = 0;
		float gap = 5;
		foreach ( var p in Panel.Children.OfType<NoticePanel>().Reverse() )
		{
			var size = p.Box.RectOuter;

			var w = p.Box.RectOuter.Width;
			var h = p.Box.RectOuter.Height + gap;

			p.UpdatePosition( new Vector2( innerBox.Right - w, innerBox.Height - y - h ) );

			if ( !p.IsDead )
			{
				y += h;
			}
		}
	}
}
