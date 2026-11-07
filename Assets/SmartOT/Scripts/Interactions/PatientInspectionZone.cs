using System;
using UnityEngine;
using SmartOT.Input;
using SmartOT.Core;

namespace SmartOT.Interactions
{
    /// <summary>
    /// Patient-specific interaction zone for the report pad inspection step.
    ///
    /// WHY THIS EXISTS INSTEAD OF USING ProximityInteractor ON Patient_Normal:
    ///   Patient_Normal is INACTIVE at scene start (it is the non-inflated model,
    ///   hidden until Phase 3 inflation). An inactive GameObject's OnTriggerEnter
    ///   callbacks never fire, so attaching any trigger detection to it is broken.
    ///
    ///   This component lives on its own always-active GameObject (Patient_InspectionZone)
    ///   and owns its own SphereCollider trigger. It is NOT part of the patient mesh.
    ///
    /// RESPONSIBILITY:
    ///   - Detects when the AutoHand Player enters/exits the zone.
    ///   - Raises OnPlayerEntered / OnPlayerExited only when "Inspect Patient" is the current task.
    ///   - Raises OnUseWhileInZone when Use is pressed while the player is inside.
    ///   - ReportPadInteraction subscribes to OnUseWhileInZone.
    ///
    /// Fix log (v2) — Task-state gating:
    ///   - Prompt and input subscription are suppressed when "Inspect Patient" is not active.
    ///   - ForceHidePrompt() unsubscribes from input so Use can no longer fire after inspection.
    ///
    /// INSPECTOR SETUP (on the Patient_InspectionZone GameObject):
    ///   - Attach this script.
    ///   - Add a SphereCollider: Is Trigger = true, Radius ≈ 1.2.
    ///   - _inputHandler → drag SmartOT_InputHandler.
    ///   - _playerTag → "Player" (must match your AutoHand Player root tag).
    ///   - _interactionPrompt → "Press Use to inspect patient".
    ///   - _taskId → "Inspect Patient" (must exactly match TaskData.taskName).
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class PatientInspectionZone : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("References")]
        [Tooltip("The SmartOTInputHandler in the scene.")]
        [SerializeField] private SmartOTInputHandler _inputHandler;

        [Header("Task Linkage")]
        [Tooltip("Must exactly match the TaskData.taskName for this interaction. " +
                 "Used to gate the prompt to the current active task only.")]
        [SerializeField] private string _taskId = "Inspect Patient";

        [Header("Settings")]
        [Tooltip("Tag that identifies the player root. Must match the AutoHand Player root tag.")]
        [SerializeField] private string _playerTag = "Player";

        [Tooltip("Text shown in the HUD when the player is inside the zone and task is active.")]
        [SerializeField] private string _interactionPrompt = "Press Use to inspect patient";

        // ── Public Events ─────────────────────────────────────────────────────
        /// <summary>Raised when the player enters the zone AND this is the current task. Arg: prompt string.</summary>
        public event Action<string> OnPlayerEntered;

        /// <summary>Raised when the player exits the zone.</summary>
        public event Action OnPlayerExited;

        /// <summary>Raised when Use is pressed while the player is inside the zone AND task is active.</summary>
        public event Action OnUseWhileInZone;

        // ── State ─────────────────────────────────────────────────────────────
        private bool _playerInZone = false;
        private bool _subscribedToInput = false;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            // Enforce trigger on the SphereCollider.
            var col = GetComponent<SphereCollider>();
            if (!col.isTrigger)
            {
                col.isTrigger = true;
                Debug.LogWarning("[PatientInspectionZone] SphereCollider was not a trigger — fixed.", this);
            }

            // Auto-find input handler if not assigned.
            if (_inputHandler == null)
            {
                _inputHandler = FindFirstObjectByType<SmartOTInputHandler>();
                if (_inputHandler == null)
                    Debug.LogError("[PatientInspectionZone] SmartOTInputHandler not found. " +
                                   "Assign it in the Inspector.", this);
                else
                    Debug.LogWarning("[PatientInspectionZone] _inputHandler auto-found. Assign it directly.", this);
            }
        }

        private void OnDisable()
        {
            if (_playerInZone)
                ExitZone();
        }

        // ── Trigger Callbacks ─────────────────────────────────────────────────
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(_playerTag)) return;
            if (_playerInZone) return;

            _playerInZone = true;

            // Only show prompt if "Inspect Patient" is currently the active task.
            if (!IsActiveTask())
            {
                Debug.Log("[PatientInspectionZone] Player entered but 'Inspect Patient' is not active — prompt suppressed.");
                return;
            }

            SubscribeToInput();
            OnPlayerEntered?.Invoke(_interactionPrompt);
            Debug.Log("[PatientInspectionZone] Player entered inspection zone. Prompt shown.");
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(_playerTag)) return;
            if (!_playerInZone) return;

            ExitZone();
            Debug.Log("[PatientInspectionZone] Player exited inspection zone. Prompt hidden.");
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Hides the prompt immediately and unsubscribes from input.
        /// Called by ReportPadInteraction after inspection is confirmed.
        /// </summary>
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
            Debug.Log($"[PatientInspectionZone] Use pressed. playerInZone={_playerInZone}, isActiveTask={IsActiveTask()}");

            if (!_playerInZone) return;
            if (!IsActiveTask())
            {
                Debug.Log("[PatientInspectionZone] Use ignored — task not active.");
                return;
            }

            OnUseWhileInZone?.Invoke();
        }
    }
}
