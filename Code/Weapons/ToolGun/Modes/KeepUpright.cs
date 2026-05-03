
[Hide]
[Title( "#tool.name.keepupright" )]
[Icon( "👆🏻" )]
[ClassName( "upright" )]
[Group( "#tool.group.constraints" )]
public class KeepUpright : ToolMode
{
	[Range( 0, 20 )]
	[Property, Sync]
	public float Hertz { get; set; } = 2.0f;

	[Range( 0, 2 )]
	[Property, Sync]
	public float DampingRatio { get; set; } = 0.7f;

	[Property, Sync, Range( 1000, 25000 ), Step( 10 )]
	public float TorqueMultiplier { get; set; } = 5000f;

	SelectionPoint _point1;
	int _stage = 0;
	const float torqueScale = 10f;

	public override string Description => _stage == 1
		? "#tool.hint.keepupright.stage1"
		: "#tool.hint.keepupright.description";

	public override string PrimaryAction => "#tool.hint.keepupright.apply_world";

	public override string SecondaryAction => _stage == 1
		? "#tool.hint.keepupright.finish"
		: "#tool.hint.keepupright.start_link";

	public override string ReloadAction => _stage == 1
		? "#tool.hint.keepupright.cancel"
		: "#tool.hint.keepupright.remove";

	protected override void OnDisabled()
	{
		base.OnDisabled();
		_stage = 0;
		_point1 = default;
	}

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		IsValidState = select.IsValid();

		if ( !IsValidState ) return;

		if ( Input.Pressed( "reload" ) )
		{
			if ( _stage == 1 )
			{
				_stage = 0;
				_point1 = default;
			}
			else
			{
				if ( !FireToolAction( ToolInput.Reload ) )
					return;

				var go = select.GameObject.Network.RootGameObject ?? select.GameObject;
				RemoveConstraints( go );
				ShootEffects( select );

				FirePostToolAction( ToolInput.Reload );
			}
			return;
		}

		if ( Input.Pressed( "attack1" ) )
		{
			if ( !FireToolAction( ToolInput.Primary ) )
				return;

			CreateWorldAnchor( select );
			ShootEffects( select );
			_stage = 0;
			_point1 = default;

			FirePostToolAction( ToolInput.Primary );
			return;
		}

		if ( Input.Pressed( "attack2" ) )
		{
			if ( _stage == 0 )
			{
				_point1 = select;
				_stage = 1;
				ShootEffects( select );
			}
			else if ( _stage == 1 )
			{
				if ( _point1.IsValid() && _point1.GameObject != select.GameObject )
				{
					if ( !FireToolAction( ToolInput.Secondary ) )
					{
						_stage = 0;
						_point1 = default;
						return;
					}

					CreateLinked( _point1, select );
					ShootEffects( select );

					FirePostToolAction( ToolInput.Secondary );
				}

				_stage = 0;
				_point1 = default;
			}
		}
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	private void CreateWorldAnchor( SelectionPoint point )
	{
		if ( !point.IsValid() ) return;

		var go = new GameObject( point.GameObject, false, "keep_upright" );
		go.LocalTransform = point.LocalTransform;
		go.Tags.Add( "constraint" );

		var mass = point.GameObject.GetComponentInParent<Rigidbody>( true )?.Mass ?? 100f;

		var joint = go.AddComponent<UprightJoint>();
		joint.Body = null;
		joint.Hertz = Hertz;
		joint.DampingRatio = DampingRatio;

		joint.MaxTorque = TorqueMultiplier * mass * torqueScale;

		go.NetworkSpawn();

		Track( go );

		var undo = Player.Undo.Create();
		undo.Name = "Upright";
		undo.Icon = "👆🏻";
		undo.Add( go );

		CheckContraptionStats( point.GameObject );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	private void CreateLinked( SelectionPoint point1, SelectionPoint point2 )
	{
		if ( !point1.IsValid() || !point2.IsValid() ) return;
		if ( point1.GameObject == point2.GameObject ) return;

		var go2 = new GameObject( point2.GameObject, false, "keep_upright" );
		go2.LocalTransform = point2.LocalTransform;
		go2.Tags.Add( "constraint" );

		var go1 = new GameObject( point1.GameObject, false, "keep_upright" );
		go1.WorldTransform = go2.WorldTransform;
		go1.Tags.Add( "constraint" );

		var cleanup = go1.AddComponent<ConstraintCleanup>();
		cleanup.Attachment = go2;

		var mass = point1.GameObject.GetComponentInParent<Rigidbody>( true )?.Mass ?? 100f;

		var joint = go1.AddComponent<UprightJoint>();
		joint.Body = go2;
		joint.Hertz = Hertz;
		joint.DampingRatio = DampingRatio;
		joint.MaxTorque = TorqueMultiplier * mass * torqueScale;

		go2.NetworkSpawn();
		go1.NetworkSpawn();

		Track( go1, go2 );

		var undo = Player.Undo.Create();
		undo.Name = "Upright";
		undo.Icon = "👆🏻";
		undo.Add( go1 );
		undo.Add( go2 );

		CheckContraptionStats( point1.GameObject );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	private void RemoveConstraints( GameObject go )
	{
		// Remove world-anchor upright joints (Body == null means no second object)
		foreach ( var joint in go.GetComponentsInChildren<UprightJoint>( true ) )
		{
			if ( !joint.Body.IsValid() )
				joint.GameObject.Destroy();
		}

		// Remove linked upright joints via ConstraintCleanup
		var builder = new LinkedGameObjectBuilder();
		builder.AddConnected( go );

		var toRemove = new List<GameObject>();
		foreach ( var linked in builder.Objects )
		{
			foreach ( var cleanup in linked.GetComponentsInChildren<ConstraintCleanup>( true ) )
			{
				if ( linked != go && cleanup.Attachment?.Root != go ) continue;
				if ( cleanup.GameObject.GetComponentInChildren<UprightJoint>( true ) is not null )
					toRemove.Add( cleanup.GameObject );
			}
		}

		foreach ( var host in toRemove )
			host.Destroy();
	}
}
