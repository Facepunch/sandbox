namespace Sandbox.Npcs;

/// <summary>
/// A designed outfit - a set of clothing to dress an NPC in.
/// </summary>
public sealed class ClothingPreset
{
	public string Name { get; set; }
	public List<Clothing> Clothing { get; set; } = [];
}

/// <summary>
/// Dresses an NPC in one of a few designed outfits and one of a few faces, each picked at random
/// when it spawns. Replaces whatever's set on the <see cref="Sandbox.Dresser"/>, which still does the
/// actual dressing.
/// </summary>
public sealed class NpcOutfits : Component
{
	/// <summary>
	/// The dresser to put the outfit on.
	/// </summary>
	[Property]
	public Dresser Dresser { get; set; }

	/// <summary>
	/// Outfits to pick from. Empty leaves the dresser as it is.
	/// </summary>
	[Property]
	public List<ClothingPreset> Outfits { get; set; } = [];

	/// <summary>
	/// Heads (skin clothing) to pick from, on top of the outfit. Empty leaves the face alone.
	/// </summary>
	[Property]
	public List<Clothing> Faces { get; set; } = [];

	protected override void OnValidate()
	{
		base.OnValidate();

		// Runs when the NPC is cloned in, before it's network spawned - so the outfit goes out in the
		// spawn snapshot and every client dresses it the same. Only in the game scene - not in the
		// editor, the cached prefab itself, thumbnails or other preview scenes, and not on clients.
		if ( Game.IsPlaying && Scene.IsValid() && !Scene.IsEditor && Scene is not PrefabScene && Scene == Game.ActiveScene && Networking.IsHost && !GameObject.Network.Active )
			PickOutfit();

		if ( Scene.IsValid() && Scene.IsEditor )
			WarnAboutClashes();
	}

	// While designing: flag outfits with items that can't be worn together - the dresser would drop one
	private void WarnAboutClashes()
	{
		foreach ( var outfit in Outfits ?? [] )
		{
			var items = outfit?.Clothing?.Where( x => x is not null ).ToList();
			if ( items is null ) continue;

			for ( var i = 0; i < items.Count; i++ )
			{
				for ( var j = i + 1; j < items.Count; j++ )
				{
					if ( !items[i].CanBeWornWith( items[j] ) )
						Log.Warning( $"{GameObject.Name}: outfit '{outfit.Name}' - {items[i].Title} can't be worn with {items[j].Title}, the dresser will drop one." );
				}
			}
		}
	}

	private void PickOutfit()
	{
		if ( !Dresser.IsValid() )
			return;

		var clothing = Dresser.Clothing.Select( x => x.Clothing ).ToList();

		if ( Outfits is { Count: > 0 } && Pick( Outfits )?.Clothing is { Count: > 0 } outfit )
			clothing = outfit.ToList();

		if ( Faces is { Count: > 0 } && Pick( Faces ) is { } face )
		{
			// One head only - the face replaces any skin the outfit came with
			clothing.RemoveAll( x => x?.Category == Clothing.ClothingCategory.Skin );
			clothing.Add( face );
		}

		// Keep the dresser's own settings (tint etc.) for anything it already had on
		var existing = Dresser.Clothing.Where( x => x.Clothing is not null ).DistinctBy( x => x.Clothing ).ToDictionary( x => x.Clothing );

		Dresser.Clothing = clothing
			.Where( x => x is not null )
			.Select( x => existing.GetValueOrDefault( x ) ?? new ClothingContainer.ClothingEntry( x ) )
			.ToList();

		MatchFacialHair( Dresser.Clothing );
	}

	/// <summary>
	/// Tint beards, moustaches and the like the same colour as the hair on their head. Each item has
	/// its own tint gradient, so match the colour rather than the tint value.
	/// </summary>
	private static void MatchFacialHair( List<ClothingContainer.ClothingEntry> entries )
	{
		var hair = entries.FirstOrDefault( x => x.Clothing is { } c && c.Category.ToString().StartsWith( "Hair" ) );
		if ( hair is null )
			return;

		var colour = hair.Clothing.TintSelection.Evaluate( hair.Tint ?? hair.Clothing.TintDefault );

		foreach ( var entry in entries )
		{
			if ( entry.Clothing is not { AllowTintSelect: true } c || !c.Category.ToString().StartsWith( "FacialHair" ) )
				continue;

			entry.Tint = ClosestTint( c.TintSelection, colour );
		}
	}

	private static float ClosestTint( Gradient gradient, Color colour )
	{
		var best = 0f;
		var bestDistance = float.MaxValue;

		for ( var t = 0f; t <= 1f; t += 0.01f )
		{
			var c = gradient.Evaluate( t );
			var distance = (c.r - colour.r) * (c.r - colour.r) + (c.g - colour.g) * (c.g - colour.g) + (c.b - colour.b) * (c.b - colour.b);
			if ( distance < bestDistance )
			{
				bestDistance = distance;
				best = t;
			}
		}

		return best;
	}

	private static T Pick<T>( List<T> list ) => list[Game.Random.Int( 0, list.Count - 1 )];
}
