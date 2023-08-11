
namespace Sandbox.UI
{
	[Library( "MenuTextEntry" )]
	public partial class MenuTextEntry : TextEntry
	{
		private float filteringMenuButtonUntil = 0;
		protected override void OnMouseDown( MousePanelEvent e )
		{
			filteringMenuButtonUntil = Time.Now + 0.25f;
			SpawnMenu.Instance.IgnoreMenuButton = true;
			base.OnMouseDown( e );
		}
		public override void OnKeyTyped( char k )
		{
			if ( k == 'q' && filteringMenuButtonUntil >= Time.Now )
			{
				filteringMenuButtonUntil = Time.Now + 0.25f;
				return;
			}
			base.OnKeyTyped( k );
		}
		protected override void OnBlur( PanelEvent e )
		{
			SpawnMenu.Instance.IgnoreMenuButton = false;
			base.OnBlur( e );
		}
	}

	namespace Construct
	{
		public static class MenuTextEntryConstructor
		{
			public static MenuTextEntry MenuTextEntry( this PanelCreator self, string text )
			{
				var control = self.panel.AddChild<MenuTextEntry>();
				control.Text = text;

				return control;
			}
		}
	}
}
