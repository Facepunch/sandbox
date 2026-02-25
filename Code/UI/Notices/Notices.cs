namespace Sandbox.UI;

public class Notices : PanelComponent
{
	protected override void OnEnabled()
	{
		base.OnEnabled();
	}

	public static Notices Current => Game.ActiveScene.Get<Notices>();

	public static NoticePanel AddNotice( string text, float seconds = 5 )
	{
		var current = Current;
		if ( current == null || current.Panel == null ) return null;

		var notice = new NoticePanel();

		notice.AddChild( new Label() { Text = text, Classes = "text", IsRich = true } );

		if ( seconds <= 0 )
			notice.Manual = true;
		else
			notice.TimeUntilDie = seconds;

		current.Panel.AddChild( notice );

		return notice;
	}

	public static NoticePanel AddNotice( string icon, Color iconColor, string text, float seconds = 5 )
	{
		var current = Current;
		if ( current == null || current.Panel == null ) return null;

		var notice = new NoticePanel();

		var iconPanel = new Label() { Text = icon, Classes = "icon" };
		iconPanel.Style.FontColor = iconColor;

		notice.AddChild( iconPanel );
		notice.AddChild( new Label() { Text = text, Classes = "text", IsRich = true } );

		if ( seconds <= 0 )
			notice.Manual = true;
		else
			notice.TimeUntilDie = seconds;

		current.Panel.AddChild( notice );

		return notice;
	}

	/// <summary>
	/// Send a notice to a specific connection. Must be called from the host.
	/// </summary>
	public static void SendNotice( Connection target, string icon, Color iconColor, string text, float seconds = 5 )
	{
		Assert.True( Networking.IsHost, "Must not be the host" );

		using ( Rpc.FilterInclude( target ) )
		{
			RpcAddNotice( icon, iconColor, text, seconds );
		}
	}

	[Rpc.Broadcast]
	private static void RpcAddNotice( string icon, Color iconColor, string text, float seconds )
	{
		AddNotice( icon, iconColor, text, seconds );
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
