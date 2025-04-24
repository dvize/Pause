using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace Pause
{
	/// <summary>
	/// Pause game mod.
	/// </summary>
	[BepInPlugin("com.dvize.pause", "PAUSE", "1.2.2")]
	public class Plugin : BaseUnityPlugin
	{
		/// <summary>
		/// Pause game shortcut.
		/// </summary>
		internal static ConfigEntry<KeyboardShortcut> TogglePause;

		/// <summary>
		/// Display "PAUSED" text.
		/// </summary>
		internal static ConfigEntry<bool> IsShowingPausedText;

		/// <summary>
		/// Logger.
		/// </summary>
		internal static ManualLogSource Log;

		/// <summary>
		/// Initializes the plugin.
		/// </summary>
		[UsedImplicitly]
		private void Awake()
		{
			Log = Logger;
			TogglePause = Config.Bind("Keybinds", "Toggle Pause", new KeyboardShortcut(KeyCode.F9));
			IsShowingPausedText = Config.Bind("General", "Show PAUSED text", true);
			Logger.LogInfo("PAUSE: Loading");

			new NewGamePatch().Enable();

			// Tick patches.
			new WorldTickPatch().Enable();
			new OtherWorldTickPatch().Enable();
			new GameTimerClassUpdatePatch().Enable();
			new TimerPanelPatch().Enable();
			new PlayerUpdatePatch().Enable();
			new EndByTimerScenarioUpdatePatch().Enable();

			// Base local game patches.
			new BaseLocalGameUpdatePatch().Enable();
		}

		/// <summary>
		/// New game patcher.
		/// </summary>
		internal class NewGamePatch : ModulePatch
		{
			/// <summary>
			/// Returns method to override.
			/// </summary>
			/// <returns> Method info. </returns>
			protected override MethodBase GetTargetMethod() => typeof(GameWorld).GetMethod(nameof(GameWorld.OnGameStarted));

			/// <summary>
			/// Initializes patch on game start.
			/// </summary>
			[PatchPrefix]
			private static void PatchPrefix()
			{
				PauseController.Enable();
			}
		}
	}
}
