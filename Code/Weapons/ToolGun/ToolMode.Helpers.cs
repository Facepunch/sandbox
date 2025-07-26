public abstract partial class ToolMode
{
	/// <summary>
	/// A point in the world selected by the current player's toolgun. We try to keep this as minimal as possible
	/// while catering for as much as possible.
	/// </summary>
	public struct SelectionPoint
	{
		public GameObject GameObject { get; set; }
		public Transform LocalTransform { get; set; }


		/// <summary>
		/// Returns true if GameObject is valid - which means we hit something
		/// </summary>
		public bool IsValid()
		{
			return GameObject.IsValid();
		}

		/// <summary>
		/// Returns the position of the hit - in the world space. This is the transformed position of the LocalTransform relative to the GameObject.
		/// </summary>
		/// <returns></returns>
		public Vector3 WorldPosition()
		{
			return GameObject.WorldTransform.PointToWorld( LocalTransform.Position );
		}

		/// <summary>
		/// Returns the transform of the hit
		/// </summary>
		/// <returns></returns>
		public Transform WorldTransform()
		{
			return GameObject.WorldTransform.ToWorld( LocalTransform );
		}

		/// <summary>
		/// Returns true if this object is a part of the static map
		/// </summary>
		public bool IsWorld => GameObject.Tags.Has( "world" );


		/// <summary>
		/// Returns true if this object is a player
		/// </summary>
		public bool IsPlayer => GameObject.Tags.Has( "player" );
	}

	/// <summary>
	/// Get a SelectionPoint from the tool gun owner's eyes.
	/// </summary>
	public SelectionPoint TraceSelect()
	{
		var player = Toolgun?.Owner;

		var tr = Scene.Trace.Ray( player.EyeTransform.ForwardRay, 4096 )
		.IgnoreGameObjectHierarchy( player.GameObject )
		.Run();

		return new SelectionPoint
		{
			GameObject = tr.GameObject,
			LocalTransform = tr.GameObject?.WorldTransform.ToLocal( new Transform( tr.EndPosition, Rotation.LookAt( tr.Normal ) ) ) ?? global::Transform.Zero
		};
	}

	/// <summary>
	/// Given a clicked point on a, and a clicked point on b, return a transform that places the objects so the points are touching
	/// </summary>
	public Transform GetEasyModePlacement( SelectionPoint a, SelectionPoint b )
	{
		var go = a.GameObject.Network.RootGameObject ?? a.GameObject;

		var tx = b.WorldTransform();
		tx.Rotation = tx.Rotation * a.LocalTransform.Rotation.Inverse * new Angles( 180, 0, 0 );
		tx.Position += tx.Rotation * -a.LocalTransform.Position * go.WorldScale;
		tx.Scale = go.WorldScale;
		return tx;
	}

}

