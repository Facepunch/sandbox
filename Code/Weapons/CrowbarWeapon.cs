using Sandbox.Rendering;
using static BaseWeapon;

public class CrowbarWeapon : BaseCarryable
{
	[Property] public float SwingDelay { get; set; } = 0.5f;
	[Property] public float MissSwingDelay { get; set; } = 0.75f;
	[Property] public SoundEvent SwingSound { get; set; }
	[Property] public SoundEvent HitSound { get; set; }
	[Property] public float Damage { get; set; } = 12.0f;

	TimeUntil timeUntilShoot = 0;

	public bool CanAttack() => timeUntilShoot <= 0;

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		if ( Input.Down( "attack1" ) )
		{
			Swing( player );
		}
	}

	public void Swing( Player player )
	{
		if ( !CanAttack() )
			return;

		var forward = player.EyeTransform.Rotation.Forward;

		var tr = Scene.Trace.Ray( player.EyeTransform.ForwardRay with { Forward = forward }, 128 )
							.IgnoreGameObjectHierarchy( player.GameObject )
							.WithCollisionRules( "bullet" )
							.Radius( 10 )
							.UseHitboxes()
							.Run();

		var dmg = Damage;

		timeUntilShoot = tr.GameObject.IsValid() ? SwingDelay : MissSwingDelay;

		SwingEffects( tr.EndPosition, tr.Hit, tr.Normal, tr.GameObject, tr.Surface );
		TraceAttack( TraceAttackInfo.From( tr, dmg, localise: false ) );

		if ( player.IsLocalPlayer )
		{
			HitMarker.CreateFromTrace( tr );
		}

		player.Controller.EyeAngles += new Angles( Random.Shared.Float( -0.2f, -0.3f ), Random.Shared.Float( -0.1f, 0.1f ), 0 );

		if ( !player.Controller.ThirdPerson && player.IsLocalPlayer )
		{
			new Sandbox.CameraNoise.Punch( new Vector3( Random.Shared.Float( -10, -15 ), Random.Shared.Float( -10, 0 ), 0 ), 1.0f, 3, 0.5f );
			new Sandbox.CameraNoise.Shake( 0.3f, 1.2f );
		}
	}

	[Rpc.Broadcast]
	public void SwingEffects( Vector3 hitpoint, bool hit, Vector3 normal, GameObject hitObject, Surface hitSurface )
	{
		if ( Application.IsDedicatedServer ) return;

		var player = Owner;
		if ( player.IsValid() )
			player.Controller.Renderer.Set( "b_attack", true );

		var ev = new IWeaponEvent.AttackEvent( ViewModel.IsValid() );
		IWeaponEvent.PostToGameObject( GameObject.Root, x => x.OnAttack( ev ) );

		GameObject.PlaySound( SwingSound );

		if ( hitObject.IsValid() )
		{
			GameObject.PlaySound( HitSound );
		}

		if ( hit )
		{

		}
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		DrawCrosshair( painter, crosshair );
	}

	protected Color CrosshairCanShoot => Color.Yellow;
	protected Color CrosshairNoShoot => Color.Red;

	public void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var len = 6;

		Color color = !CanAttack() ? CrosshairNoShoot : CrosshairCanShoot;

		hud.SetBlendMode( BlendMode.Lighten );
		hud.DrawCircle( center, len, color );
	}
}
