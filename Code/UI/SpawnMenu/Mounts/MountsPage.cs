using Sandbox.Mounting;

/// <summary>
/// This component has a kill icon that can be used in the killfeed, or somewhere else.
/// </summary>
[Title( "#spawnmenu.tab.games" ), Order( 2050 ), Icon( "🧩" )]
public class MountsPage : BaseSpawnMenu
{
	Dictionary<string, SpawnMenuOption> mountOptions = new ();

	protected override void OnVisibilityChanged()
	{
		base.OnVisibilityChanged();

		if ( IsVisible )
		{
			UpdateMounts();
		}
	}

	protected override void Rebuild()
	{
		var all = Sandbox.Mounting.Directory.GetAll().ToArray();
		var available = all.Where( x => x.Available ).ToArray();
		var unavailable = all.Where( x => !x.Available ).ToArray();

		if ( available.Any() )
		{
			AddHeader( "#spawnmenu.section.local" );

			foreach ( var entry in available.OrderBy( x => x.Title ) )
			{
				var o = AddOption( entry.Title, () => new MountContent() { Ident = entry.Ident } );
				o.Enabled = entry.Mounted;

				mountOptions[entry.Ident] = o;
			}
		}

		if ( unavailable.Any() )
		{
			AddHeader( "#spawnmenu.section.not_installed" );
			
			foreach ( var entry in unavailable.OrderBy( x => x.Title ) )
			{
				var o = AddOption( entry.Title, null );
				o.Enabled = false;

				mountOptions[entry.Ident] = o;
			}
		}
	}

	private void UpdateMounts()
	{
		foreach ( var entry in Sandbox.Mounting.Directory.GetAll() )
		{
			if ( mountOptions.TryGetValue( entry.Ident, out var o ) )
			{
				o.Enabled = entry.Mounted;
			}
		}

		StateHasChanged();
	}
}
