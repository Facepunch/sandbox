using Sandbox.Rendering;

public class MeleeWeapon : BaseCarryable
{
	/// <summary>
	/// Cooldown after a hit connects.
	/// </summary>
	[Property] public float SwingDelay { get; set; } = 0.5f;

	/// <summary>
	/// Cooldown after a swing misses.
	/// </summary>
	[Property] public float MissSwingDelay { get; set; } = 0.75f;

	/// <summary>
	/// Damage dealt per hit.
	/// </summary>
	[Property] public float Damage { get; set; } = 12f;

	/// <summary>
	/// Reach of the swing trace.
	/// </summary>
	[Property] public float Range { get; set; } = 128f;

	/// <summary>
	/// Radius of the swing trace sphere.
	/// </summary>
	[Property] public float SwingRadius { get; set; } = 10f;

	/// <summary>
	/// Physics impulse magnitude applied to hit objects.
	/// </summary>
	[Property] public float SwingForce { get; set; } = 1000f;

	[Property] public SoundEvent SwingSound { get; set; }
	[Property] public SoundEvent HitSound { get; set; }

	TimeUntil timeUntilSwing = 0;

	public bool CanAttack() => timeUntilSwing <= 0;

	protected virtual bool WantsPrimaryAttack() => Input.Down( "attack1" );

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		if ( WantsPrimaryAttack() )
			Swing( player );
	}

	public void Swing( Player player )
	{
		if ( !CanAttack() )
			return;

		var forward = AimRay.Forward;

		var tr = Scene.Trace.Ray( AimRay with { Forward = forward }, Range )
							.IgnoreGameObjectHierarchy( AimIgnoreRoot )
							.WithoutTags( "playercontroller" )
							.Radius( SwingRadius )
							.UseHitboxes()
							.Run();

		timeUntilSwing = tr.GameObject.IsValid() ? SwingDelay : MissSwingDelay;

		SwingEffects( tr.EndPosition, tr.Hit, tr.Normal, tr.GameObject, tr.Surface );
		TraceAttack( TraceAttackInfo.From( tr, Damage, localise: false ) );

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

		if ( ViewModel.IsValid() )
			ViewModel.RunEvent<ViewModel>( x => x.OnAttack() );
		else if ( WorldModel.IsValid() )
			WorldModel.RunEvent<WorldModel>( x => x.OnAttack() );

		GameObject.PlaySound( SwingSound );

		if ( hitObject.IsValid() )
			GameObject.PlaySound( HitSound );

		if ( !hit || !hitObject.IsValid() )
			return;

		var prefab = hitSurface.PrefabCollection.BulletImpact ?? hitSurface.GetBaseSurface()?.PrefabCollection.BulletImpact;

		// Still null?
		if ( prefab is null )
			return;

		var fwd = Rotation.LookAt( normal * -1.0f, Vector3.Random );

		var impact = prefab.Clone();
		impact.WorldPosition = hitpoint;
		impact.WorldRotation = fwd;
		impact.SetParent( hitObject, true );

		if ( hitObject.GetComponentInChildren<SkinnedModelRenderer>() is not { CreateBoneObjects: true } skinned )
			return;

		// find closest bone
		var bones = skinned.GetBoneTransforms( true );

		var closestDist = float.MaxValue;

		for ( var i = 0; i < bones.Length; i++ )
		{
			var bone = bones[i];
			var dist = bone.Position.Distance( hitpoint );
			if ( dist < closestDist )
			{
				closestDist = dist;
				impact.SetParent( skinned.GetBoneObject( i ), true );
			}
		}
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		DrawCrosshair( painter, crosshair );
	}

	public virtual void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var len = 6;
		Color color = CanAttack() ? Color.White : Color.Red;

		hud.SetBlendMode( BlendMode.Lighten );
		hud.DrawCircle( center, len, color );
	}
}
