using Sandbox;
using System;
using System.Collections.Generic;

namespace Sandbox;

public class BloodSystem : GameObjectSystem<BloodSystem>
{
	private List<DecalDefinition> _mainSplatter;
	private List<DecalDefinition> _dripSplatters;
	private uint _bloodSortLayer = 0; 

	public BloodSystem( Scene scene ) : base( scene )
	{
	}

	private void InitializeResources()
	{
		if ( _dripSplatters != null ) return;

		var main = ResourceLibrary.Get<DecalDefinition>( "decals/blood/blood_splatter_01.decal" );
		_mainSplatter = main != null ? new() { main } : null;

		_dripSplatters = new()
		{
			ResourceLibrary.Get<DecalDefinition>( "decals/blood/blood_splatter_02.decal" ),
			ResourceLibrary.Get<DecalDefinition>( "decals/blood/blood_splatter_03.decal" ),
			ResourceLibrary.Get<DecalDefinition>( "decals/blood/blood_splatter_04.decal" )
		};
		_dripSplatters.RemoveAll( x => x == null ); // Quick cleanup
	}

	public void SpawnBlood( Vector3 hitPosition, Vector3 direction, float damage = 50.0f )
	{
		if ( damage < 5.0f ) return;

		InitializeResources();

		int dropCount = (int)Math.Clamp( damage / 15.0f, 1.0f, 10.0f );
		bool isMajorDamage = damage >= 35.0f;

		for ( int i = 0; i < dropCount; i++ )
		{
			bool isMainDrop = (i == 0); 

			var resourceList = (isMainDrop && isMajorDamage && _mainSplatter != null) 
				? _mainSplatter 
				: _dripSplatters;

			if ( resourceList == null || resourceList.Count == 0 ) continue;

			var traceDir = -direction;
			if ( traceDir.LengthSquared <= 0.01f ) traceDir = Vector3.Down;

			if ( !isMainDrop )
			{
				traceDir = (traceDir + Vector3.Random * Game.Random.Float( 0.2f, 0.8f )).Normal;
			}

			var startPos = hitPosition + (Vector3.Up * 5f);
			const float BloodEjectDistance = 256.0f;
			
			var tr = Scene.Trace.Ray( new Ray( startPos, traceDir ), BloodEjectDistance )
				.WithoutTags( "player", "ragdoll" )
				.Run();

			// Fallback to floor if it misses the wall
			if ( !tr.Hit )
			{
				tr = Scene.Trace.Ray( new Ray( startPos, Vector3.Down ), BloodEjectDistance )
					.WithoutTags( "player", "ragdoll" )
					.Run();
					
				if ( !tr.Hit ) continue;
			}

			var gameObject = Scene.CreateObject();

			if ( tr.GameObject.IsValid() )
			{
				gameObject.SetParent( tr.GameObject, true );
			}

			gameObject.WorldPosition = tr.HitPosition;
			gameObject.WorldRotation = Rotation.LookAt( -tr.Normal ) * Rotation.FromAxis( Vector3.Forward, Game.Random.Float( 0, 360 ) );

			var decal = gameObject.AddComponent<Decal>();
			decal.Decals = resourceList;
			decal.Transient = true;
			decal.SortLayer = _bloodSortLayer++;

			float size = isMainDrop 
				? Math.Clamp( damage * 0.2f, 3.0f, 8.0f ) 
				: Game.Random.Float( 2.5f, 5.0f );

			decal.Size = new Vector3( size, size, size );

			var fader = gameObject.AddComponent<DecalFader>();
			fader.Lifetime = 15.0f; 
			fader.FadeTime = 5.0f;  
		}
	}
}

/// <summary>
/// Smoothly fades out a decal over time and then destroys its GameObject.
/// Could be reusable for blood, scorch marks, and bullet holes.
/// </summary>
public sealed class DecalFader : Component
{
	public float Lifetime { get; set; } = 15.0f;
	public float FadeTime { get; set; } = 5.0f;
	public Color BaseColor { get; set; } = Color.White;

	private Decal _decal;
	private TimeSince _timeSinceSpawned;
	private float _startFadeTime;

	protected override void OnStart()
	{
		_timeSinceSpawned = 0;
		_decal = Components.Get<Decal>();
		
		_startFadeTime = Lifetime - FadeTime;

		if ( _decal.IsValid() )
		{
			_decal.ColorTint = BaseColor;
		}
	}

	protected override void OnUpdate()
	{
		if ( _timeSinceSpawned >= Lifetime )
		{
			GameObject.Destroy();
			return;
		}

		if ( _timeSinceSpawned < _startFadeTime ) return;
		
		if ( _decal.IsValid() )
		{
			float fadeFraction = (_timeSinceSpawned - _startFadeTime) / FadeTime;
			_decal.ColorTint = BaseColor.WithAlpha( BaseColor.a * (1.0f - fadeFraction) );
		}
	}
}
