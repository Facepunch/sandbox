using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Tests;
using System.Threading.Tasks;

[Library, UseTemplate]
public partial class CloudModelList : Panel
{
	public VirtualScrollPanel Canvas { get; set; }

	public CloudModelList()
	{
	}

	protected override void PostTemplateApplied()
	{
		base.PostTemplateApplied();

		Canvas.Layout.AutoColumns = true;
		Canvas.Layout.ItemWidth = 100;
		Canvas.Layout.ItemHeight = 100;

		Canvas.OnCreateCell = ( cell, data ) =>
		{
			var file = (Package)data;
			var panel = cell.Add.Panel( "icon" );
			panel.AddEventListener( "onclick", () => ConsoleSystem.Run( "spawn", file.FullIdent ) );
			panel.Style.BackgroundImage = Texture.Load( file.Thumb );
		};

		_ = UpdateItems();
	}

	public async Task UpdateItems( int offset = 0 )
	{
		var q = new Package.Query();
		q.Type = Package.Type.Model;
		q.Order = Package.Order.Newest;
		q.Take = 200;
		q.Skip = offset;

		var found = await q.RunAsync( default );

		Log.Info( found );
		Canvas.SetItems( found );

		// TODO - auto add more items here
	}

	public void RefreshItems()
	{
		Canvas.Clear();
		_ = UpdateItems();
	}

}
