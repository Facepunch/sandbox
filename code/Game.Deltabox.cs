using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deltabox
{
	partial class SandboxGame : Game
	{
		[ServerCmd( "player_show", Help = "Hides the player" )]
		public static void DrawPlayer()
		{
			var owner = ConsoleSystem.Caller?.Pawn;

			setPlayerAlpha( 100, owner );
		}

		[ServerCmd( "player_hide", Help = "Shows the player" )]
		public static void HidePlayer()
		{
			var owner = ConsoleSystem.Caller?.Pawn;

			setPlayerAlpha( 0, owner );
		}

		private static void setPlayerAlpha( int alpha, Entity player )
		{
			ModelEntity playerModel = player as ModelEntity;
			Color playerCrolor = playerModel.RenderColor;

			playerCrolor.a = alpha;

			for ( int i = 0; i < playerModel.Children.Count; i++ )
			{
				if ( playerModel.Children[i] is ModelEntity )
				{
					ModelEntity model = playerModel.Children[i] as ModelEntity;
					model.RenderColor = playerCrolor;
				}
			}

			playerModel.RenderColor = playerCrolor;
		}
	}
}
