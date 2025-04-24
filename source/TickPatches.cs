using EFT;
using EFT.UI.BattleTimer;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using System.Runtime.CompilerServices;
using TMPro;

namespace Pause
{
	/// <summary>
	/// Patch for "GameWorld.DoWorldTick" method.
	/// </summary>
	public class WorldTickPatch : ModulePatch
	{
		/// <summary>
		/// Returns method to override.
		/// </summary>
		/// <returns> Method info. </returns>
		protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(GameWorld), nameof(GameWorld.DoWorldTick));

		/// <summary>
		/// Check if need to process player world ticks.
		/// </summary>
		/// <param name="__instance"> Game world instance. </param>
		/// <param name="dt"> Player tick. </param>
		/// <returns> Is game world being processed. </returns>
		[PatchPrefix]
		internal static bool Prefix(GameWorld __instance, float dt)
		{
			if (!PauseController.IsPaused)
			{
				return true;
			}

			// Invoking the PlayerTick to prevent hand jank.
			typeof(GameWorld).GetMethod("PlayerTick", BindingFlags.Instance | BindingFlags.Public)
				?.Invoke(__instance, new object[] { dt });

			return false;
		}
	}

	/// <summary>
	/// Patch for "GameWorld.DoOtherWorldTick" method.
	/// </summary>
	public class OtherWorldTickPatch : ModulePatch
	{
		/// <summary>
		/// Returns method to override.
		/// </summary>
		/// <returns> Method info. </returns>
		protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(GameWorld), nameof(GameWorld.DoOtherWorldTick));

		/// <summary>
		/// Check if need to process other worlds ticks.
		/// </summary>
		/// <param name="__instance"> Game world instance. </param>
		/// <returns> Is game world being processed. </returns>
		[PatchPrefix]
		internal static bool Prefix(GameWorld __instance)
		{
			// It looks like this just calls the player's update ticks.
			return !PauseController.IsPaused;
		}
	}

	/// <summary>
	/// Patch for "GameTimerClass".
	/// </summary>
	public class GameTimerClassUpdatePatch : ModulePatch
	{
		/// <summary>
		/// Returns method to override.
		/// </summary>
		/// <returns> Method info. </returns>
		protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(GameTimerClass), nameof(GameTimerClass.method_0));

		/// <summary>
		/// Process before game timer method.
		/// </summary>
		/// <returns> Is game timer being processed. </returns>
		[PatchPrefix]
		internal static bool Prefix()
		{
			return !PauseController.IsPaused;
		}
	}

	/// <summary>
	/// Patch for "TimerPanel.UpdateTimer" method.
	/// </summary>
	public class TimerPanelPatch : ModulePatch
	{
		/// <summary>
		/// Returns method to override.
		/// </summary>
		/// <returns> Method info. </returns>
		protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(TimerPanel), nameof(TimerPanel.UpdateTimer));

		/// <summary>
		/// Process before timer panel update.
		/// </summary>
		/// <param name="____timerText"> Game timer UI text. </param>
		/// <returns> Is timer panel being processed. </returns>
		[PatchPrefix]
		internal static bool Prefix(TextMeshProUGUI ____timerText)
		{
			// Patch for 'fake' game ui timer when you press "o".
			// Set the text to PAUSED for fun.
			if (!PauseController.IsPaused)
			{
				return true;
			}

			if (Plugin.IsShowingPausedText.Value)
			{
				____timerText.SetMonospaceText("PAUSED", false);
			}

			return false;
		}
	}

	/// <summary>
	/// Patch for "Player.UpdateTick" method.
	/// </summary>
	public class PlayerUpdatePatch : ModulePatch
	{
		/// <summary>
		/// Returns method to override.
		/// </summary>
		/// <returns> Method info. </returns>
		protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player), nameof(Player.UpdateTick));

		/// <summary>
		/// Process before updating player's tick.
		/// </summary>
		/// <returns> Is tick processed. </returns>
		[PatchPrefix]
		internal static bool Prefix()
		{
			return !PauseController.IsPaused;
		}
	}

	/// <summary>
	/// Patch for "EndByTimerScenario.Update" method.
	/// </summary>
	public class EndByTimerScenarioUpdatePatch : ModulePatch
	{
		/// <summary>
		/// Returns method to override.
		/// </summary>
		/// <returns> Method info. </returns>
		protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(EndByTimerScenario), nameof(EndByTimerScenario.Update));

		/// <summary>
		/// Process before updating timer expiration.
		/// </summary>
		/// <returns> Is timer processed. </returns>
		[PatchPrefix]
		internal static bool Prefix()
		{
			return !PauseController.IsPaused;
		}
	}
}
