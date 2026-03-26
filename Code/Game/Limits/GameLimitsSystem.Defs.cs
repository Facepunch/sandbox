public sealed partial class GameLimitsSystem
{
	/// <summary>
	/// Maps SteamId to per-category counts.
	/// </summary>
	class Count
	{
		private readonly Dictionary<string, int> _data = new();

		public int Get( string category ) => _data.GetValueOrDefault( category );
		public void Increment( string category ) => _data[category] = Get( category ) + 1;
		public void Decrement( string category ) => _data[category] = Math.Max( 0, Get( category ) - 1 );
		public IEnumerator<KeyValuePair<string, int>> GetEnumerator() => _data.GetEnumerator();
	}

	/// <summary>
	/// Maps a <see cref="LimitCategory"/> constant to its ConVar name.
	/// </summary>
	public sealed record LimitDef( string Category, string ConVar );

	/// <summary>
	/// All numeric limit definitions. Add one entry here to introduce a new limit category.
	/// </summary>
	public static readonly IReadOnlyList<LimitDef> Limits = new LimitDef[]
	{
		new( LimitCategory.Prop,       "sb.limits.props" ),
		new( LimitCategory.Entity,     "sb.limits.entities" ),
		new( LimitCategory.Thruster,   "sb.limits.thrusters" ),
		new( LimitCategory.Balloon,    "sb.limits.balloons" ),
		new( LimitCategory.Wheel,      "sb.limits.wheels" ),
		new( LimitCategory.Constraint, "sb.limits.constraints" ),
	};

	/// <summary>
	/// Returns the configured limit for the given category, or <c>-1</c> if unrecognised.
	/// </summary>
	public static int GetLimit( string category ) => category switch
	{
		LimitCategory.Prop        => MaxProps,
		LimitCategory.Entity      => MaxEntities,
		LimitCategory.Thruster    => MaxThrusters,
		LimitCategory.Balloon     => MaxBalloons,
		LimitCategory.Wheel       => MaxWheels,
		LimitCategory.Constraint  => MaxConstraints,
		_                         => -1,
	};

	/// <summary>
	/// Returns the <see cref="LimitCategory"/> for a ConVar name, or <c>null</c> if none.
	/// </summary>
	public static string GetCategoryForConVar( string convar ) =>
		Limits.FirstOrDefault( l => l.ConVar == convar )?.Category;
}
