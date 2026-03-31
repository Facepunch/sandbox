namespace Sandbox.UI;

public class Hints : GameObjectSystem<Hints>
{
	[Title( "Show UI Hints" )]
	[ConVar( "cl_showhints", ConVarFlags.Saved | ConVarFlags.GameSetting, Help = "Whether to display popup hints." )]
	public static bool cl_showhints { get; set; } = true;

	record class Hint( string Name, string Icon, RealTimeUntil Delay )
	{
		public bool Ready => Delay < 0;
	}

	List<Hint> _queue = new();

	public Hints( Scene scene ) : base( scene )
	{
		Queue( "openspawnmenu", "ℹ️", 10 );
		Queue( "openinspectmenu", "ℹ️", 40 );

		Listen( Stage.StartUpdate, 0, Tick, "UpdateHints" );
	}

	public void Queue( string hintName, string hintIcon, float delay )
	{
		var hint = new Hint( hintName, hintIcon, delay );
		_queue.Add( hint );
	}

	RealTimeSince timeSinceLast = 0;

	void Tick()
	{
		if ( timeSinceLast < 3 )
			return;

		if ( !cl_showhints )
			return;

		var next = _queue.Where( x => x.Ready ).FirstOrDefault();
		if ( next is null ) return;

		_queue.Remove( next );
		timeSinceLast = 0;

		var phrase = Game.Language.GetPhrase( $"hint.{next.Name}" );
		phrase = ReplaceSpecialTokens( phrase );

		Notices.AddNotice( next.Icon, Color.White, phrase, 5 );
	}

	public void Cancel( string hintName )
	{
		_queue.RemoveAll( x => x.Name.Equals( hintName, StringComparison.OrdinalIgnoreCase ) );
	}

	string ReplaceSpecialTokens( string input )
	{
		if ( !input.Contains( '{' ) ) return input;
		if ( !input.Contains( '}' ) ) return input;

		// replace {input:<inputname>} with the key bound to that input
		{
			input = System.Text.RegularExpressions.Regex.Replace( input,
				@"{input:([^}]+)}",
				match =>
				{
					string key = match.Groups[1].Value.Trim();
					return $"<span class=\"key\"> {Input.GetButtonOrigin( key )} </span>";
				} );
		}

		return input;
	}
}
