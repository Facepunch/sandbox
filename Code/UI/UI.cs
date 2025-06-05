public sealed class UI : Component
{
	[ConVar( "sbdm.ui", ConVarFlags.Cheat ), Change( nameof( OnIsEnabledChanged ) )]
	public static bool IsEnabled { get; set; } = true;

	private static UI Current { get; set; }

	static void OnIsEnabledChanged( bool before, bool after )
	{
		if ( Current.IsValid() )
		{
			Current.Update();
		}
	}

	protected override void OnStart()
	{
		Current = this;
		Update();
	}

	private void Update()
	{
		foreach ( var x in GetComponentsInChildren<PanelComponent>( true ) )
		{
			x.Enabled = IsEnabled;
		}
	}
}
