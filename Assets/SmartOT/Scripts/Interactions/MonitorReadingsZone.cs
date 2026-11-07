using System;
using UnityEngine;
using SmartOT.Input;
using SmartOT.Core;
using Autohand;

namespace SmartOT.Interactions
{
    /// <summary>
    /// Patient monitor readings zone for the "Record Monitor Readings" task.
    ///
    /// Mirrors PatientInspectionZone but for the monitor readings step:
    ///   1. Player grabs the report pad (AutoHand).
    ///   2. Player approaches the patient monitor — this zone detects entry.
    ///   3. Prompt "Press Use to record monitor readings" appears (task-gated).
    ///   4. Player presses Use while holding the pad → task completes.
    ///
    /// Place this on a dedicated always-active GO near the patient monitor.
    /// Do NOT attach to the monitor mesh itself (may be a prefab with mesh colliders).
    ///
    /// Inspector setup:
    ///   - Add SphereCollider: Is Trigger = true, Radius ≈ 1.2.
    ///   - _inputHandler     → drag SmartOT_InputHandler.
    ///   - _reportPad        → drag the Report Pad Grabbable GameObject.
    ///   - _taskId           → "Record Monitor Readings" (default).
    ///   - _interactionPrompt → "Press Use to record monitor readings".
    ///   - _playerTag        → "Player".
    ///
    /// Also add PatientZoneUIBridge (or a MonitorZoneUIBridge equivalent) to wire
    /// OnPlayerEntered/OnPlayerExited to TaskUIManager.ShowPrompt/HidePrompt.
    /// Use MonitorZoneUIBridge (see UI folder) instead of PatientZoneUIBridge.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    [AddComponentMenu("SmartOT/Monitor Readings Zone")]
    public class MonitorReadingsZone : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("References")]
        [Tooltip("The SmartOTInputHandler in the scene.")]
        [SerializeField] private SmartOTInputHandler _inputHandler;

        [Tooltip("The Report Pad Grabbable. Needed to verify the player is holding it.")]
        [SerializeField] private Grabbable _reportPad;

        [Header("Task Linkage")]
        [Tooltip("Must exactly match the TaskData.taskName.")]
        [SerializeField] private string _taskId = "Record Monitor Readings";

        [Header("Settings")]
        [SerializeField] private string _playerTag = "Player";
        [SerializeField] private string _interactionPrompt = "Press Use to record monitor readings";

        // ── Public Events ─────────────────────────────────────────────────────
        /// <summary>Raised when the player enters the zone AND task is active. Arg: prompt string.</summary>
        public event Action<string> OnPlayerEntered;

        /// <summary>Raised when the player exits the zone.</summary>
        public event Action OnPlayerExited;

        // ── State ─────────────────────────────────────────────────────────────
        private bool _playerInZone = false;
        private bool _subscribedToInput = false;
        private bool _readingsRecorded = false;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            var col = GetComponent<SphereCollider>();
            if (!col.isTrigger)
            {
                col.isTrigger = true;
                Debug.LogWarning("[MonitorReadingsZone] SphereCollider was not a trigger — fixed.", this);
            }

            if (_inputHandler == null)
            {
                _inputHandler = FindFirstObjectByType<SmartOTInputHandler>();
                if (_inputHandler == null)
                    Debug.LogError("[MonitorReadingsZone] SmartOTInputHandler not found. Assign in Inspector.", this);
            }

            if (_reportPad == null)
                Debug.LogError("[MonitorReadingsZone] _reportPad not assigned. Drag the Report Pad here.", this);
        }

        private void OnDisable()
        {
            if (_playerInZone) ExitZone();
        }

        // ── Trigger Callbacks ─────────────────────────────────────────────────
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(_playerTag)) return;
            if (_playerInZone) return;

            _playerInZone = true;

            if (!IsActiveTask())
            {
                Debug.Log("[MonitorReadingsZone] Player entered but task not active — prompt suppressed.");
                return;
            }

            SubscribeToInput();
            OnPlayerEntered?.Invoke(_interactionPrompt);
            Debug.Log("[MonitorReadingsZone] Player entered. Prompt shown.");
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(_playerTag)) return;
            if (!_playerInZone) return;

            ExitZone();
            Debug.Log("[MonitorReadingsZone] Player exited. Prompt hidden.");
        }

        // ── Public API ────────────────────────────────────────────────────────
        public void ForceHidePrompt()
        {
            UnsubscribeFromInput();
            OnPlayerExited?.Invoke();
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private bool IsActiveTask()
        {
            if (string.IsNullOrWhiteSpace(_taskId)) return false;
            var mgr = TaskProgressionManager.Instance;
            if (mgr == null) return false;
            return mgr.IsCurrentTask(_taskId.Trim());
        }

        private void ExitZone()
        {
            _playerInZone = false;
            UnsubscribeFromInput();
            OnPlayerExited?.Invoke();
        }

        private void SubscribeToInput()
        {
            if (_inputHandler != null && !_subscribedToInput)
            {
                _inputHandler.OnUseStarted += HandleUse;
                _subscribedToInput = true;
            }
        }

        private void UnsubscribeFromInput()
        {
            if (_inputHandler != null && _subscribedToInput)
            {
                _inputHandler.OnUseStarted -= HandleUse;
                _subscribedToInput = false;
            }
        }

        private void HandleUse()
        {
            if (!_playerInZone) return;
            if (!IsActiveTask()) { Debug.Log("[MonitorReadingsZone] Use ignored — task not active."); return; }
            if (_readingsRecorded) { Debug.Log("[MonitorReadingsZone] Readings already recorded."); return; }

            // Require the report pad to be held.
            if (_reportPad == null || !_reportPad.IsHeld())
            {
                Debug.Log("[MonitorReadingsZone] Use pressed but report pad is not held — ignoring.");
                return;
            }

            _readingsRecorded = true;
            Debug.Log("[MonitorReadingsZone] Monitor readings recorded! Completing task.");

            ForceHidePrompt();
            TaskProgressionManager.Instance?.CompleteTask(_taskId.Trim());
        }
    }
}
