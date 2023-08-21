using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sandbox
{
	public class Job
	{
		public string id;

		public string model;

		public Action<SandboxPlayer> giveWeaponsToPlayer { get; set; }
	}
}
