using Sandbox.UI;

namespace Sandbox.Tools
{
	[Library( "tool_whatisthat", Title = "What is that?", Description = "Prop identificationifier (prints to console because what is chat?)", Group = "construction" )]
	public partial class WhatIsThatTool : BaseTool
	{
		public override void Simulate()
		{
			if ( !Game.IsServer )
				return;

			using ( Prediction.Off() )
			{
				if ( Input.Pressed( "attack1" ) )
				{
					var tr = DoTrace();

					if ( !tr.Hit )
						return;

					if ( !tr.Entity.IsValid() )
						return;

					var attached = !tr.Entity.IsWorld && tr.Body.IsValid() && tr.Body.PhysicsGroup != null && tr.Body.GetEntity().IsValid();

					if ( attached && tr.Entity is not Prop )
						return;
					var prop = tr.Entity as Prop;

					CreateHitEffects( tr.EndPosition, tr.Normal );
					// todo: how do print to chat/etc?
					Log.Info( "That is a: " + prop.Model + " (" + prop.NetworkIdent + ") weighing " + prop.PhysicsBody.Mass );
				}
			}
		}
	}
}
