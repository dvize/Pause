using EFT;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Pause
{
	/// <summary>
	/// Patch for "EftGamePlayerOwner.Update" method.
	/// </summary>
	public class BaseLocalGameUpdatePatch : ModulePatch
	{
		/// <summary>
		/// Returns method to override.
		/// </summary>
		/// <returns> Method info. </returns>
		protected override MethodBase GetTargetMethod() 
		{
			return typeof(BaseLocalGame<EftGamePlayerOwner>).GetMethod("Update", BindingFlags.Public | BindingFlags.Instance);
		} 

		/// <summary>
		/// Processes player owner update.
		/// </summary>
		/// <returns> Is processed. </returns>
		[PatchPrefix]
		internal static bool Prefix()
		{
			return !PauseController.IsPaused;
		}
	}
}
