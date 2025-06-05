using Sandbox.Utility;

public sealed class HitMarker : Component
{
	TimeSince timeSinceBorn;

	[Property] public Color Color { get; set; } = Color.White;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		timeSinceBorn = 0;
	}

	protected override void OnPreRender()
	{
		if ( Scene.Camera is null ) return;

		var delta = (timeSinceBorn.Relative % 2).Remap( 0, 0.4f );
		if ( delta >= 1 )
		{
			DestroyGameObject();
			return;
		}

		delta = Easing.EaseIn( delta );

		var hud = Scene.Camera.Hud;
		var pos = Scene.Camera.PointToScreenPixels( WorldPosition, out bool isBehind );

		if ( isBehind )
			return;

		var left = Vector2.Left + Vector2.Up;
		var up = Vector2.Left - Vector2.Up;

		var color = Color.WithAlpha( 1 - MathF.Pow( delta, 4 ) );

		var t = 3;
		var a = 2;
		var b = 8;
		var corners = new Vector4( 100 );

		hud.DrawLine( pos + left * a, pos + left * b, t, color, corners );
		hud.DrawLine( pos - left * a, pos - left * b, t, color, corners );
		hud.DrawLine( pos + up * a, pos + up * b, t, color, corners );
		hud.DrawLine( pos - up * a, pos - up * b, t, color, corners );
	}

	public static void CreateFromTrace( SceneTraceResult tr )
	{
		if ( !tr.Hit ) return;

		var hitPlayer = tr.GameObject.GetComponent<Player>();

		if ( !hitPlayer.IsValid() )
			return;

		bool wasHeadshot = tr.Hitbox?.Tags?.Contains( "head" ) ?? false;
		CreateAt( tr.HitPosition, wasHeadshot, hitPlayer.Armour > 0f );
	}

	public static void CreateAt( Vector3 position, bool isHeadshot = false, bool hasArmor = false )
	{
		var prefab = isHeadshot ? "items/hitmarker/hitmarker.head.prefab" : "items/hitmarker/hitmarker.prefab";
		if ( hasArmor ) prefab = "items/hitmarker/hitmarker.armor.prefab";

		var go = GameObject.Clone( prefab );
		go.WorldPosition = position;
	}
}
