using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Animations;
using EFT.UI.BattleTimer;
using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Pause
{
	/// <summary>
	/// Game pause controller.
	/// </summary>
	public class PauseController : MonoBehaviour
	{
		#region Properties

		/// <summary>
		/// Is game paused.
		/// </summary>
		internal static bool IsPaused { get; private set; }

		#endregion Properties

		#region Fields: Private

		/// <summary>
		/// Paused game on.
		/// </summary>
		private DateTime? _pausedDate;

		/// <summary>
		/// Resumed game on.
		/// </summary>
		private DateTime? _unpausedDate;

		/// <summary>
		/// Game timer class.
		/// Controls actual raid time.
		/// </summary>
		private GameTimerClass _gameTimerClass;

		/// <summary>
		/// Main timer panel.
		/// Controls on screen raid time.
		/// </summary>
		private MainTimerPanel _mainTimerPanel;

		/// <summary>
		/// Abstract game.
		/// </summary>
		private AbstractGame _abstractGame;

		/// <summary>
		/// Paused audio sources.
		/// </summary>
		private List<AudioSource> _pausedAudioSources;

		#endregion Fields: Private

		#region Fields: Static

		/// <summary>
		/// Logger.
		/// </summary>
		internal static ManualLogSource Logger;

		/// <summary>
		/// Game world.
		/// Used to determine if game is in the right state to be able to pause.
		/// </summary>
		private static GameWorld GameWorld;

		/// <summary>
		/// Player.
		/// </summary>
		private static Player MainPlayer;

		/// <summary>
		/// Mouse look control field info.
		/// </summary>
		private static FieldInfo MouseLookControlField;

		/// <summary>
		/// "Is aiming" field info.
		/// </summary>
		private static FieldInfo IsAimingField;

		/// <summary>
		/// Start time field info.
		/// </summary>
		private static FieldInfo StartTimeField;

		/// <summary>
		/// Escape time field info.
		/// </summary>
		private static FieldInfo EscapeTimeField;

		/// <summary>
		/// Timer panel field info.
		/// </summary>
		private static FieldInfo TimerPanelField;

		/// <summary>
		/// Game time field info.
		/// </summary>
		private static FieldInfo GameDateTimeField;

		#endregion Fields: Static

		/// <summary>
		/// Initializes mod.
		/// </summary>
		[UsedImplicitly]
		private void Awake()
		{
			Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(PauseController));

			IsPaused = false;
			_abstractGame = Singleton<AbstractGame>.Instance;
			_mainTimerPanel = FindObjectOfType<MainTimerPanel>();
			_gameTimerClass = _abstractGame?.GameTimer;
			_pausedAudioSources = new List<AudioSource>();

			MouseLookControlField = AccessTools.Field(typeof(Player), "MouseLookControl");
			IsAimingField = AccessTools.Field(typeof(ProceduralWeaponAnimation), "_isAiming");
			StartTimeField = AccessTools.Field(typeof(GameTimerClass), "nullable_0");
			EscapeTimeField = AccessTools.Field(typeof(GameTimerClass), "nullable_1");
			TimerPanelField = AccessTools.Field(typeof(TimerPanel), "dateTime_0");
			GameDateTimeField = AccessTools.Field(typeof(GameDateTime), "_realtimeSinceStartup");
		}

		/// <summary>
		/// Processes instance destruction.
		/// </summary>
		[UsedImplicitly]
		private void OnDestroy()
		{
			IsPaused = false;
			GameWorld = null;
			MainPlayer = null;
			Logger = null;
			_pausedAudioSources.Clear();
			MouseLookControlField = null;
			StartTimeField = null;
			EscapeTimeField = null;
			TimerPanelField = null;
			GameDateTimeField = null;
		}

		/// <summary>
		/// Processes update.
		/// </summary>
		[UsedImplicitly]
		private void Update()
		{
			if (!IsKeyPressed(Plugin.TogglePause.Value))
			{
				return;
			}

			IsPaused = !IsPaused;

			if (IsPaused)
			{
				Pause();
			}
			else
			{
				Unpause();
				ResetFov();
			}
		}

		/// <summary>
		/// Pauses the game.
		/// </summary>
		private void Pause()
		{
			Time.timeScale = 0f;
			_pausedDate = DateTime.UtcNow;

			MainPlayer.enabled = false;
			MainPlayer.PauseAllEffectsOnPlayer();

			foreach (var player in GetPlayers().Where(p => !p.IsYourPlayer))
			{
				Logger.LogInfo($"Deactivating player: {player.name}");
				SetPlayerState(player, false);
			}

			PauseAllAudio();

			if (Plugin.IsShowingPausedText.Value)
			{
				ShowTimer();
			}
		}

		/// <summary>
		/// Resumes the game.
		/// </summary>
		private void Unpause()
		{
			Time.timeScale = 1f;
			_unpausedDate = DateTime.UtcNow;

			MainPlayer.enabled = true;
			MainPlayer.UnpauseAllEffectsOnPlayer();

			foreach (var player in GetPlayers().Where(p => !p.IsYourPlayer))
			{
				Logger.LogInfo($"Reactivating player: {player.name}");
				SetPlayerState(player, true);
			}

			ResumeAllAudio();

			if (Plugin.IsShowingPausedText.Value && !_mainTimerPanel.ForcePull)
			{
				StartCoroutine(CoHideTimer());
			}

			UpdateTimers(GetTimePaused());
		}

		/// <summary>
		/// Pauses all audio instances.
		/// </summary>
		private void PauseAllAudio()
		{
			_pausedAudioSources.Clear();
			foreach (var audioSource in FindObjectsOfType<AudioSource>().Where(s => s.isPlaying))
			{
				audioSource.Pause();
				_pausedAudioSources.Add(audioSource);
			}
		}

		/// <summary>
		/// Resumes all audio instances.
		/// </summary>
		private void ResumeAllAudio()
		{
			foreach (var audioSource in _pausedAudioSources)
			{
				audioSource.UnPause();
			}

			_pausedAudioSources.Clear();
		}

		/// <summary>
		/// Returns all players.
		/// </summary>
		/// <returns> Players list. </returns>
		private static IEnumerable<Player> GetPlayers()
		{
			return GameWorld?.AllAlivePlayersList ?? new List<Player>();
		}

		/// <summary>
		/// Sets player state.
		/// </summary>
		/// <param name="player"> Player. </param>
		/// <param name="active"> Is active. </param>
		private static void SetPlayerState(Player player, bool active)
		{
			if (player == null)
			{
				return;
			}

			if (player.PlayerBones != null)
			{
				foreach (var r in player.PlayerBones.GetComponentsInChildren<Rigidbody>())
				{
					r.velocity = Vector3.zero;
					r.angularVelocity = Vector3.zero;

					if (active)
					{
						r.WakeUp();
					}
					else
					{
						r.Sleep();
					}
				}
			}
			
			var weaponRigidBody = player.HandsController?.ControllerGameObject?.GetComponent<Rigidbody>();
			if (weaponRigidBody != null)
			{
				weaponRigidBody.angularVelocity = Vector3.zero;
				weaponRigidBody.velocity = Vector3.zero;
				weaponRigidBody.Sleep();
			}

			if (!active)
			{
				player.AIData.BotOwner.DecisionQueue.Clear();
				player.gameObject.SetActive(false);
			}
			else
			{
				player.gameObject.SetActive(true);
				player.AIData.BotOwner.CalcGoal();
			}
		}

		/// <summary>
		/// Shows timer.
		/// </summary>
		private void ShowTimer()
		{
			_mainTimerPanel?.DisplayTimer();
		}

		/// <summary>
		/// Hides timer.
		/// </summary>
		/// <returns> Currently processed timer panel. </returns>
		private IEnumerator CoHideTimer()
		{
			if (_mainTimerPanel == null)
			{
				yield break;
			}

			yield return new WaitForSeconds(4f);
			_mainTimerPanel.HideTimer();
		}

		/// <summary>
		/// Returns time spent in paused state.
		/// </summary>
		/// <returns> Time spent in paused state. </returns>
		private TimeSpan GetTimePaused()
		{
			return _pausedDate.HasValue && _unpausedDate.HasValue
				? _unpausedDate.Value - _pausedDate.Value
				: TimeSpan.Zero;
		}

		/// <summary>
		/// Updates timers.
		/// </summary>
		/// <param name="timePaused"> Time spent in paused state. </param>
		private void UpdateTimers(TimeSpan timePaused)
		{
			// Safely retrieve values using reflection.
			var startDate = StartTimeField.GetValue(_gameTimerClass) as DateTime?;
			var escapeDate = TimerPanelField.GetValue(_mainTimerPanel) as DateTime?;
			var timerPanelDate = TimerPanelField.GetValue(_mainTimerPanel) as DateTime?;
			var realTimeSinceStartup = GameDateTimeField.GetValue(GameWorld.GameDateTime) as float?;

			if (!startDate.HasValue
				|| !escapeDate.HasValue
				|| !timerPanelDate.HasValue
				|| !realTimeSinceStartup.HasValue)
			{
				return;
			}

			// Adjust DateTime values.
			var adjustedStartDate = startDate.Value.Add(timePaused);
			var adjustedEscapeDate = escapeDate.Value.Add(timePaused);
			var adjustedTimerPanelDate = timerPanelDate.Value.Add(timePaused);
			var adjustedRealTime = realTimeSinceStartup.Value + (float)timePaused.TotalSeconds;

			// Set updated values back.
			StartTimeField.SetValue(_gameTimerClass, adjustedStartDate);
			EscapeTimeField.SetValue(_gameTimerClass, adjustedEscapeDate);
			TimerPanelField.SetValue(_mainTimerPanel, adjustedTimerPanelDate);
			GameDateTimeField.SetValue(GameWorld.GameDateTime, adjustedRealTime);
		}

		/// <summary>
		/// Resets field of view.
		/// </summary>
		private static void ResetFov()
		{
			if (MainPlayer == null
				|| MainPlayer.ProceduralWeaponAnimation == null
				|| CameraClass.Instance == null)
			{
				return;
			}

			var baseFov = MainPlayer.ProceduralWeaponAnimation.Single_2;
			var targetFov = baseFov;
			var mouseLookControlPlayer = MouseLookControlField?.GetValue(typeof(Player)) as Player;
			var isAiming = (bool)(IsAimingField?.GetValue(MainPlayer.ProceduralWeaponAnimation) ?? false);
			var scopeAimTransformsCount = MainPlayer.ProceduralWeaponAnimation.ScopeAimTransforms?.Count ?? 0;

			if (MainPlayer.ProceduralWeaponAnimation.PointOfView != EPointOfView.FirstPerson
				|| mouseLookControlPlayer == null
				|| mouseLookControlPlayer.MouseLookControl
				|| MainPlayer.ProceduralWeaponAnimation.AimIndex >= scopeAimTransformsCount)
			{
				return;
			}

			if (isAiming)
			{
				targetFov = MainPlayer.ProceduralWeaponAnimation.CurrentScope?.IsOptic ?? false
					? 35f
					: baseFov - 15f;
			}

			Logger.LogDebug($"Current FOV (When Unpausing): {CameraClass.Instance.Fov}, Base FOV: {baseFov}, Target FOV: {targetFov}");
			CameraClass.Instance.SetFov(targetFov, 1f, !isAiming);
		}

		/// <summary>
		/// Enables mod.
		/// </summary>
		internal static void Enable()
		{
			if (!Singleton<IBotGame>.Instantiated)
			{
				return;
			}

			GameWorld = Singleton<GameWorld>.Instance;
			GameWorld.GetOrAddComponent<PauseController>();
			MainPlayer = GameWorld.MainPlayer;
			Logger.LogDebug("PauseController enabled.");
		}

		/// <summary>
		/// Processes keyboard shortcut input.
		/// </summary>
		/// <param name="key"> Keyboard shortcut. </param>
		/// <returns> Is key pressed. </returns>
		internal static bool IsKeyPressed(KeyboardShortcut key)
		{
			return UnityInput.Current.GetKeyDown(key.MainKey)
				&& key.Modifiers.All(modifier => UnityInput.Current.GetKey(modifier));
		}
	}
}
