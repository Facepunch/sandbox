/// <summary>
/// Keeps a prop parented onto a bone of a skinned model
/// </summary>
public sealed class BoneMerge : Component
{
	/// <summary>
	/// The bone the prop is parented to, so it can be moved onto the matching bone of a ragdoll.
	/// </summary>
	[Property, Sync] public string BoneName { get; set; }

	/// <summary>
	/// Prop that has been merged
	/// </summary>
	public GameObject Prop => GameObject.Parent;

	bool _superseded;

	protected override void OnStart()
	{
		if ( IsProxy ) return;

		if ( Prop?.Parent is null or Sandbox.Scene )
			GameObject.Destroy();
	}

	protected override void OnDestroy()
	{
		if ( IsProxy || _superseded ) return;

		var prop = Prop;
		if ( !prop.IsValid() || prop.IsDestroyed ) return;

		prop.SetParent( null, true );
		SetPhysics( prop, true );
	}

	/// <summary>
	/// Parent <paramref name="prop"/> onto a bone of <paramref name="target"/> at <paramref name="world"/>, with its own physics off.
	/// Host only. Returns the link object, which undo destroys to let the prop go.
	/// </summary>
	public static GameObject Attach( GameObject prop, SkinnedModelRenderer target, string boneName, Transform world )
	{
		var boneObject = FindBoneObject( target, boneName );
		if ( !boneObject.IsValid() ) return null;

		// Merging an already merged prop moves it, its old link goes without letting it go
		foreach ( var old in OwnLinks( prop ) )
		{
			old._superseded = true;
			old.GameObject.Destroy();
		}

		prop.SetParent( boneObject, false );
		prop.WorldTransform = world;
		SetPhysics( prop, false );

		var link = new GameObject( prop, true, "bone merge" );
		link.AddComponent<BoneMerge>().BoneName = boneName;
		link.NetworkSpawn();

		return link;
	}

	static GameObject FindBoneObject( SkinnedModelRenderer target, string boneName )
	{
		if ( !target.IsValid() || target.Model?.Bones.HasBone( boneName ) != true ) return null;

		if ( !target.CreateBoneObjects )
		{
			target.CreateBoneObjects = true;
			target.GameObject.Network.Refresh( target );
		}

		return target.GetBoneObject( boneName );
	}

	static void SetPhysics( GameObject prop, bool enabled )
	{
		foreach ( var rb in prop.GetComponentsInChildren<Rigidbody>( true ) )
			rb.Enabled = enabled;

		// Colliders pick their body when they're enabled
		foreach ( var collider in prop.GetComponentsInChildren<Collider>() )
		{
			collider.Enabled = false;
			collider.Enabled = true;
		}
	}

	/// <summary>
	/// Move bone merged props from one renderer to another
	/// </summary>
	public static void MoveAll( SkinnedModelRenderer from, SkinnedModelRenderer to )
	{
		if ( !from.IsValid() || !to.IsValid() ) return;

		foreach ( var link in from.GameObject.GetComponentsInChildren<BoneMerge>().ToList() )
		{
			var prop = link.Prop;
			var boneObject = FindBoneObject( to, link.BoneName );

			if ( !prop.IsValid() || !boneObject.IsValid() )
			{
				link.GameObject.Destroy();
				continue;
			}

			var local = prop.LocalTransform;
			prop.SetParent( boneObject, false );
			prop.LocalTransform = local;
		}
	}

	/// <summary>
	/// Let go of every prop merged onto <paramref name="root"/>
	/// </summary>
	public static void ReleaseAll( GameObject root )
	{
		if ( !root.IsValid() ) return;

		foreach ( var link in root.GetComponentsInChildren<BoneMerge>().ToList() )
			link.GameObject.Destroy();
	}

	/// <summary>
	/// Let go of <paramref name="prop"/> if it's merged, leaving anything merged onto it alone. Host only.
	/// </summary>
	public static void Release( GameObject prop )
	{
		foreach ( var link in OwnLinks( prop ) )
			link.GameObject.Destroy();
	}

	/// <summary>
	/// Whether <paramref name="prop"/> itself is merged onto a bone.
	/// </summary>
	public static bool IsMerged( GameObject prop ) => OwnLinks( prop ).Any();

	static IEnumerable<BoneMerge> OwnLinks( GameObject prop )
	{
		if ( !prop.IsValid() ) return [];

		return prop.Children.Select( x => x.GetComponent<BoneMerge>() ).Where( x => x.IsValid() ).ToList();
	}
}
