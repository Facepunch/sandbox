namespace Sandbox.Tools
{
	[Library( "tool_weld2", Title = "Weld (Bad)", Description = "Weld stuff and ragdolls together\nWill break!", Group = "construction" )]
	public partial class WeldTool2 : BaseTool
	{
		private Prop target;
		private int targetBone;

		public override void Simulate()
		{
			if ( !Host.IsServer )
				return;

			//Bad code beyond this point!!!
			using ( Prediction.Off() )
			{
				var startPos = Owner.EyePos;
				var dir = Owner.EyeRot.Forward;

				var tr = Trace.Ray( startPos, startPos + dir * MaxTraceDistance )
					.Ignore( Owner )
					.Run();

				if ( !tr.Hit || !tr.Body.IsValid() || !tr.Entity.IsValid() || tr.Entity.IsWorld )
				{
					//Log.Info( $"1 TYPE: {tr.Entity.GetType()}" );
					return;
				}

				if ( tr.Entity.PhysicsGroup == null /*|| tr.Entity.PhysicsGroup.BodyCount > 1*/ )
				{
					//Log.Info( $"2 TYPE: {tr.Entity.GetType()}" );
					return;
				}

				if ( tr.Entity is not Prop prop )
				{
					//Log.Info($"3 TYPE: {tr.Entity.GetType()}");
					return;
				}

				if ( Input.Pressed( InputButton.Attack1 ) )
				{
					if ( prop.Root is not Prop rootProp )
					{
						//Log.Info( $"4 TYPE: {prop.GetType()}" );
						return;
					}

					//Don't weld the target to it self
					if ( target == rootProp )
					{
						//og.Info( $"5 TYPE: {rootProp.GetType()}" );
						return;
					}

					//if we don't have a target, get one
					if ( !target.IsValid() )
					{
						//Log.Info( $"6 TYPE: {rootProp.GetType()}, PhysBodyCount: {rootProp.PhysicsGroup.BodyCount}" );
						if ( rootProp.PhysicsGroup.BodyCount > 1 )
						{
							//Log.Info( "RAGDOLL" );
							target = rootProp;
							targetBone = tr.Bone;
						}
						else
						{
							//Log.Info( "PROP" );
							target = rootProp;
						}
					}
					//if we have a target weld it to what we are pointing at
					else
					{
						/*if ( target.PhysicsGroup.BodyCount > 1 )
						{
							Log.Info( $"TARGET: {target.GetType()}, IS RAGDOLL" );
						}
						else
						{
							Log.Info( $"TARGET: {target.GetType()}, IS PROP" );
						}*/

						//Make sure to unfreeze the ragdoll first
						if ( target.PhysicsGroup.BodyCount > 1 )
						{
							//Log.Info( $"Welding Ragdoll to prop" );
							target.SetParent( rootProp );
							target = null;
						}
						else
						{
							if ( rootProp.PhysicsGroup.BodyCount > 1 )
							{
								//Log.Info( $"Welding Prop to ragdoll" );
								target.SetParent( rootProp, targetBone );
								target = null;
							}
							else
							{
								//Log.Info( $"Welding Prop to prop" );
								target.Weld( rootProp );
								target = null;
							}
						}
					}

					//Log.Info($"TARGET = {target.GetModelName()}");
				}
				else if ( Input.Pressed( InputButton.Attack2 ) )
				{
					prop.Unweld( true );

					Reset();
				}
				else if ( Input.Pressed( InputButton.Reload ) )
				{
					if ( prop.Root is not Prop rootProp )
					{
						return;
					}

					rootProp.Unweld();

					Reset();
				}
				else
				{
					return;
				}

				CreateHitEffects( tr.EndPos );
			}
		}

		private void Reset()
		{
			target = null;
		}

		public override void Activate()
		{
			base.Activate();

			Reset();
		}

		public override void Deactivate()
		{
			base.Deactivate();

			Reset();
		}
	}
}
