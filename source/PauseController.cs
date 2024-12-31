using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Animations;
using EFT.CameraControl;
using EFT.UI.BattleTimer;
using HarmonyLib;
using UnityEngine;
using static EFT.Player;

namespace Pause
{
    public class PauseController : MonoBehaviour
    {
        internal static bool IsPaused { get; private set; } = false;

        private DateTime? _pausedDate;
        private DateTime? _unpausedDate;

        private static GameWorld _gameWorld;
        private static Player _mainPlayer;

        private GameTimerClass _gameTimerClass;
        private MainTimerPanel _mainTimerPanel;
        private AbstractGame _abstractGame;

        private static FieldInfo _mouseLookControlField;

        private static FieldInfo _startTimeField;
        private static FieldInfo _escapeTimeField;
        private static FieldInfo _timerPanelField;
        private static FieldInfo _gameDateTimeField;

        private static FieldInfo _firearmAnimationDataField;
        private static FieldInfo _isAimingField;

        internal static ManualLogSource Logger;

        private List<AudioSource> _pausedAudioSources;

        private void Awake()
        {
            Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(PauseController));

            IsPaused = false;
            _abstractGame = Singleton<AbstractGame>.Instance;
            _mainTimerPanel = GameObject.FindObjectOfType<MainTimerPanel>();
            _gameTimerClass = _abstractGame?.GameTimer;

            _mouseLookControlField = AccessTools.Field(typeof(Player), "MouseLookControl");
            _firearmAnimationDataField = AccessTools.Field(typeof(ProceduralWeaponAnimation), "_firearmAnimationData");
            _isAimingField = AccessTools.Field(typeof(ProceduralWeaponAnimation), "_isAiming");

            _startTimeField = AccessTools.Field(typeof(GameTimerClass), "nullable_0");
            _escapeTimeField = AccessTools.Field(typeof(GameTimerClass), "nullable_1");
            _timerPanelField = AccessTools.Field(typeof(TimerPanel), "dateTime_0");
            _gameDateTimeField = AccessTools.Field(typeof(GameDateTime), "_realtimeSinceStartup");


            _pausedAudioSources = new List<AudioSource>();
        }

        private void Update()
        {
            if (IsKeyPressed(Plugin.TogglePause.Value))
            {
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
        }

        private void Pause()
        {

            Time.timeScale = 0f;
            _pausedDate = DateTime.UtcNow;

            _mainPlayer.enabled = false;
            _mainPlayer.PauseAllEffectsOnPlayer();

            foreach (var player in GetPlayers())
            {
                if (player.IsYourPlayer) continue;

                Logger.LogInfo($"Deactivating player: {player?.name}");
                SetPlayerState(player, false);
            }

            PauseAllAudio();
            ShowTimer();
        }

        private void Unpause()
        {

            Time.timeScale = 1f;
            _unpausedDate = DateTime.UtcNow;

            _mainPlayer.enabled = true;
            _mainPlayer.UnpauseAllEffectsOnPlayer();

            foreach (var player in GetPlayers())
            {
                if (player.IsYourPlayer) continue;

                Logger.LogInfo($"Reactivating player: {player?.name}");
                SetPlayerState(player, true);
            }

            ResumeAllAudio();
            StartCoroutine(CoHideTimer());

            UpdateTimers(GetTimePaused());
        }

        private void PauseAllAudio()
        {
            _pausedAudioSources.Clear();
            foreach (var audioSource in FindObjectsOfType<AudioSource>())
            {
                if (audioSource.isPlaying)
                {
                    audioSource.Pause();
                    _pausedAudioSources.Add(audioSource);
                }
            }
        }

        private void ResumeAllAudio()
        {
            foreach (var audioSource in _pausedAudioSources)
            {
                audioSource.UnPause();
            }
            _pausedAudioSources.Clear();
        }

        private IEnumerable<Player> GetPlayers() => _gameWorld?.AllAlivePlayersList ?? new List<Player>();

        private void SetPlayerState(Player player, bool active)
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

        private void ShowTimer()
        {
            _mainTimerPanel?.DisplayTimer();
        }

        private IEnumerator CoHideTimer()
        {
            if (_mainTimerPanel != null)
            {
                yield return new WaitForSeconds(4f);
                _mainTimerPanel.HideTimer();
            }
        }

        private TimeSpan GetTimePaused()
        {
            if (_pausedDate.HasValue && _unpausedDate.HasValue)
            {
                return _unpausedDate.Value - _pausedDate.Value;
            }
            return TimeSpan.Zero;
        }

        private void UpdateTimers(TimeSpan timePaused)
        {
            // Safely retrieve values using reflection
            var startDate = _startTimeField.GetValue(_gameTimerClass) as DateTime?;
            var escapeDate = _escapeTimeField.GetValue(_gameTimerClass) as DateTime?;
            var timerPanelDate = _timerPanelField.GetValue(_mainTimerPanel) as DateTime?;
            var realTimeSinceStartup = (float?)_gameDateTimeField.GetValue(_gameWorld.GameDateTime);

            if (startDate.HasValue && escapeDate.HasValue && timerPanelDate.HasValue && realTimeSinceStartup.HasValue)
            {
                // Adjust DateTime values
                var adjustedStartDate = startDate.Value.Add(timePaused);
                var adjustedEscapeDate = escapeDate.Value.Add(timePaused);
                var adjustedTimerPanelDate = timerPanelDate.Value.Add(timePaused);
                var adjustedRealTime = realTimeSinceStartup.Value + (float)timePaused.TotalSeconds;

                // Set updated values back
                _startTimeField.SetValue(_gameTimerClass, adjustedStartDate);
                _escapeTimeField.SetValue(_gameTimerClass, adjustedEscapeDate);
                _timerPanelField.SetValue(_mainTimerPanel, adjustedTimerPanelDate);
                _gameDateTimeField.SetValue(_gameWorld.GameDateTime, adjustedRealTime);
            }
        }
        private void ResetFov()
        {
            if (_mainPlayer == null || _mainPlayer.ProceduralWeaponAnimation == null)
                return;

            float baseFOV = _mainPlayer.ProceduralWeaponAnimation.Single_2;
            float targetFOV = baseFOV;

            var firearmAnimationData = _mouseLookControlField.GetValue(typeof(Player)) as Player;
            var isAiming = (bool)_isAimingField.GetValue(_mainPlayer.ProceduralWeaponAnimation);

            if (_mainPlayer.ProceduralWeaponAnimation.PointOfView == EPointOfView.FirstPerson && firearmAnimationData != null)
            {
                if (!(_mainPlayer.ProceduralWeaponAnimation.AimIndex >= _mainPlayer.ProceduralWeaponAnimation.ScopeAimTransforms.Count || firearmAnimationData.MouseLookControl))
                {
                    if (isAiming)
                    {
                        targetFOV = _mainPlayer.ProceduralWeaponAnimation.CurrentScope.IsOptic ? 35f : (baseFOV - 15f);
                    }

                    // Log what the current FOV is 
#if DEBUG
            Logger.LogWarning($"Current FOV (When Unpausing): {CameraClass.Instance.Fov} Base FOV: {baseFOV} Target FOV: {targetFOV}");
#endif
                    CameraClass.Instance.SetFov(targetFOV, 1f, !isAiming);
                }
            }
        }

        private void OnDestroy()
        {
            IsPaused = false;
            _gameWorld = null;
            _mainPlayer = null;
            Logger = null;
            _pausedAudioSources.Clear();
            _mouseLookControlField = null;
            _startTimeField = null;
            _escapeTimeField = null;
            _timerPanelField = null;
            _gameDateTimeField = null;
        }

        internal static void Enable()
        {
            if (Singleton<IBotGame>.Instantiated)
            {
                _gameWorld = Singleton<GameWorld>.Instance;
                _gameWorld.GetOrAddComponent<PauseController>();
                _mainPlayer = _gameWorld.MainPlayer;
                Logger.LogDebug("PauseController enabled.");
            }
        }

        internal static bool IsKeyPressed(KeyboardShortcut key)
        {
            if (!UnityInput.Current.GetKeyDown(key.MainKey)) return false;
            return key.Modifiers.All(modifier => UnityInput.Current.GetKey(modifier));
        }

        internal static bool IsKeyPressed(KeyCode key) => UnityInput.Current.GetKeyDown(key);
    }
}
