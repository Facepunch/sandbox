using System;

namespace Sandbox
{
	/// <summary>
	/// Library Classes implementing this interface will be automatically Initialize()'d on game boot
	/// </summary>
	public interface IAutoload : IDisposable
	{
		/// <summary>
		/// On hotload, should this class be reinitialized
		/// </summary>
		bool ReloadOnHotload { get => false; }

		void Initialize();
	}
}
