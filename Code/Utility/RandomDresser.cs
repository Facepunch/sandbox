/// <summary>
/// Quick and nasty class to randomly dress a citizen using <see cref="Sandbox.Dresser"/>
/// </summary>
public sealed class RandomDresser : Component
{
	[Property]
	public Dresser Dresser { get; set; }

	[Button]
	public void Dress()
	{
		Dresser.Clear();
		Dresser.Clothing.Clear();

		AddFrom( Clothing.ClothingCategory.Hat, Clothing.ClothingCategory.HatCap, Clothing.ClothingCategory.Hair, Clothing.ClothingCategory.HairShort, Clothing.ClothingCategory.HairLong, Clothing.ClothingCategory.HairMedium );
		AddFrom( Clothing.ClothingCategory.Trousers, Clothing.ClothingCategory.Shorts, Clothing.ClothingCategory.Underwear, Clothing.ClothingCategory.Skirt );
		AddFrom( Clothing.ClothingCategory.Shoes, Clothing.ClothingCategory.Boots );
		AddFrom( Clothing.ClothingCategory.Shirt, Clothing.ClothingCategory.TShirt, Clothing.ClothingCategory.Tops );

		if ( Game.Random.Float() < 0.5f )
			AddFrom( [Clothing.ClothingCategory.Jacket, Clothing.ClothingCategory.Vest, Clothing.ClothingCategory.Coat, Clothing.ClothingCategory.Cardigan, Clothing.ClothingCategory.Coat] );

		if ( Game.Random.Float() < 0.5f )
			AddFrom( Clothing.ClothingCategory.GlassesEye, Clothing.ClothingCategory.GlassesSun );

		if ( Game.Random.Float() < 0.2f )
			AddFrom( Clothing.ClothingCategory.Gloves );

		if ( Game.Random.Float() < 0.3f )
			AddFrom( Clothing.ClothingCategory.FacialHairBeard, Clothing.ClothingCategory.FacialHairGoatee, Clothing.ClothingCategory.FacialHairSideburns );

		Dresser.Apply();
	}

	public void AddFrom( params List<Clothing.ClothingCategory> categories ) => AddFrom( Game.Random.FromList( categories ) );

	public void AddFrom( Clothing.ClothingCategory category )
	{
		var clothing = ResourceLibrary.GetAll<Clothing>().Where( x => x.Category.Equals( category ) ).ToList();
		var rand = Game.Random.FromList( clothing );

		if ( !rand.IsValid() )
			return;

		Dresser.Clothing.Add( new ClothingContainer.ClothingEntry()
		{
			Clothing = rand,
		} );

		Dresser.ManualTint = Game.Random.Float( 0, 1f );
		Dresser.ManualAge = Game.Random.Float( 0, 1f );
		Dresser.ManualHeight = Game.Random.Float( 0.5f, 1f );
	}

	protected override void OnStart()
	{
		Dress();
	}
}
