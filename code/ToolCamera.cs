using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sandbox
{
	public class ToolCamera : Camera
	{
		public Entity Owner { get; set; }
		private static bool drawHud { get; set; } = false;

		public override void Activated()
		{
			//needed to work
			FieldOfView = 90;
		}

		public override void Deactivated()
		{
			base.Deactivated();

			//Always turn on player hud when we deactivate
			Local.Hud?.SetClass( "devcamera", false );
		}

		public override void Update()
		{
			if ( Owner == null )
			{
				return;
			}

			Pos = Owner.Position;
			Rot = Owner.Rotation;

			//Control player hud when viewing through the camera
			Local.Hud?.SetClass( "devcamera", drawHud );
		}

		[ClientCmd("cam_togglehud", Help = "Toggles the HUD when viewing from the camera tool")]
		public static void SetHud()
		{
			drawHud = !drawHud;
		}

		[ClientCmd( "cam_setfov", Help = "Set the fov of the camera tool\nUsage: cam_setfov <number>\nExample: cam_setfov 90" )]
		public static void SetFov( int fov )
		{
			ToolCamera cam = (Local.Pawn as SandboxPlayer).MainCamera as ToolCamera;

			//FOV does not work at extreme values
			if ( fov == 0 )
			{
				cam.FieldOfView = 1;
				return;
			}

			if ( fov == 180 )
			{
				cam.FieldOfView = 179;
				return;
			}

			cam.FieldOfView = fov;
		}
	}

}
