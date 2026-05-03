using Sandbox.UI;

[Hide]
[Title( "#tool.name.emitter" )]
[Icon( "💨" )]
[ClassName( "emittertool" )]
[Group( "#tool.group.building" )]
public class EmitterTool : ToolMode
{
	public override bool UseSnapGrid => true;
	public override IEnumerable<string> TraceIgnoreTags => ["constraint", "collision"];

	/// <summary>
	/// The physical emitter body to spawn (model + physics).
	/// </summary>
	[Property, ResourceSelect( Extension = "smemit", AllowPackages = true ), Title( "Base" )]
	public string BaseDef { get; set; } = "entities/emitter/basic.smemit";

	/// <summary>
	/// The particle/VFX effect the emitter will produce.
	/// </summary>
	[Property, ResourceSelect( Extension = "semit", AllowPackages = true ), Title( "Effect" )]
	public string EffectDef { get; set; } = "entities/particles/sparks.semit";

	public override string Description => "#tool.hint.emittertool.description";

	protected override void OnStart()
	{
		base.OnStart();

		RegisterAction( ToolInput.Primary, () => "#tool.hint.emittertool.place", OnPlace );
	}

	void OnPlace()
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var baseDef = ResourceLibrary.Get<ScriptedEmitterModel>( BaseDef );
		if ( baseDef == null ) return;

		var pos = select.WorldTransform();
		var placementTrans = new Transform( pos.Position );
		placementTrans.Rotation = pos.Rotation;

		var effectDef = ResourceLibrary.Get<ScriptedEmitter>( EffectDef );
		Spawn( select, baseDef.Prefab, effectDef, placementTrans );
		ShootEffects( select );
	}

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var baseDef = ResourceLibrary.Get<ScriptedEmitterModel>( BaseDef );
		if ( baseDef == null ) return;

		var pos = select.WorldTransform();
		var placementTrans = new Transform( pos.Position );
		placementTrans.Rotation = pos.Rotation;

		DebugOverlay.GameObject( baseDef.Prefab.GetScene(), transform: placementTrans, castShadows: true, color: Color.White.WithAlpha( 0.9f ) );
	}

	[Rpc.Host]
	public void Spawn( SelectionPoint point, PrefabFile emitterPrefab, ScriptedEmitter effect, Transform tx )
	{
		if ( emitterPrefab == null )
			return;

		var go = emitterPrefab.GetScene().Clone();
		go.Tags.Add( "removable" );
		go.Tags.Add( "constraint" );
		go.WorldTransform = tx;

		var emitter = go.GetComponent<EmitterEntity>( true );
		if ( emitter.IsValid() && effect != null )
		{
			emitter.Emitter = effect;
		}

		if ( !point.IsWorld )
		{
			var joint = go.AddComponent<FixedJoint>();
			joint.Attachment = Joint.AttachmentMode.LocalFrames;
			joint.LocalFrame2 = point.GameObject.WorldTransform.WithScale( 1 ).ToLocal( tx );
			joint.LocalFrame1 = new Transform();
			joint.AngularFrequency = 0;
			joint.LinearFrequency = 0;
			joint.Body = point.GameObject;
			joint.EnableCollision = false;
		}

		ApplyPhysicsProperties( go );

		go.NetworkSpawn( true, null );

		Track( go );

		var undo = Player.Undo.Create();
		undo.Name = "Emitter";
		undo.Icon = "💨";
		undo.Add( go );

		CheckContraptionStats( point.GameObject );
	}
}
