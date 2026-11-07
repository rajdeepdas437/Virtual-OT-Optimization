using System;
using UnityEngine;
using SmartOT.Input;
using SmartOT.Core;

namespace SmartOT.Interactions
{
    /// <summary>
    /// Proximity-based interaction detector for equipment GameObjects.
    ///
    /// Fix log (v2):
    ///   - Added ForceHidePrompt() so EquipmentInteraction can clear the prompt immediately.
    ///   - Full debug logging added.
    ///   - _inputHandler fallback search improved with clear error.
    ///
    /// Fix log (v3) — Task-state gating:
    ///   - OnTriggerEnter now checks IsCurrentTask() before showing the prompt.
    ///     If this interaction is not the current task (or is already done), no prompt
    ///     is shown and no input subscription is made.
    ///   - HandleUse re-checks IsCurrentTask() before firing OnInteractionTriggered.
    ///   - OnTriggerEnter is re-evaluated on every entry so if the task becomes active
    ///     while the player is standing inside, they must step out and back in.
    ///     (This is the simplest approach with no Update loop.)
    ///   - _taskId must be set to exactly match the TaskData.taskName for this equipment.
    ///
    /// Inspector setup:
    ///   - _inputHandler: drag SmartOT_InputHandler from the scene.
    ///   - _taskId: exact task name this equipment belongs to (e.g. "Activate Anesthesia Machine").
    ///   - _interactionPrompt: text shown in the HUD when in range and task is active.
    ///   - _playerTag: must match the AutoHand Player root tag (default: "Player").
    ///   - Collider must have Is Trigger checked. Recommended radius: 1.5 m.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ProximityInteractor : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("References")]
        [Tooltip("The SmartOTInputHandler in the scene.")]
        [SerializeField] private SmartOTInputHandler _inputHandler;

        [Header("Task Linkage")]
        [Tooltip("Must exactly match the TaskData.taskName for this equipment's task. " +
                 "Used to gate prompt display and input to the current active task only.")]
        [SerializeField] private string _taskId;

        [Header("Prompt")]
        [Tooltip("Text shown in the HUD when the player is in range AND this is the current task.")]
        [SerializeField] private string _interactionPrompt = "Press E to interact";

        [Header("Settings")]
        [Tooltip("Tag that identifies the player. Must match your AutoHand Player root tag.")]
        [SerializeField] private string _playerTag = "Player";

        // ── Public Events ─────────────────────────────────────────────────────
        /// <summary>Raised when the player steps into the zone AND this is the current task. Arg: prompt string.</summary>
        public event Action<string> OnPlayerEntered;

        /// <summary>Raised when the player leaves the zone.</summary>
        public event Action OnPlayerExited;

        /// <summary>Raised when Use is pressed while the player is in the zone AND this is the current task.</summary>
        public event Action OnInteractionTriggered;

        // ── State ─────────────────────────────────────────────────────────────
        private bool _playerInRange = false;
        private bool _subscribedToInput = false;

        public bool PlayerInRange => _playerInRange;
        public string InteractionPrompt => _interactionPrompt;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            if (_inputHandler == null)
            {
                _inputHandler = FindFirstObjectByType<SmartOTInputHandler>();

                if (_inputHandler == null)
                    Debug.LogError($"[ProximityInteractor] '{gameObject.name}': SmartOTInputHandler not found. " +
                                   "Assign it in the Inspector (drag SmartOT_InputHandler GO).", this);
                else
                    Debug.LogWarning($"[ProximityInteractor] '{gameObject.name}': _inputHandler auto-found. " +
                                     "Assign it directly for reliability.", this);
            }

            if (string.IsNullOrWhiteSpace(_taskId))
                Debug.LogWarning($"[ProximityInteractor] '{gameObject.name}': _taskId is empty. " +
                                 "This interactor will never show its prompt. Set _taskId in the Inspector.", this);

            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                col.isTrigger = true;
                Debug.LogWarning($"[ProximityInteractor] '{gameObject.name}' collider was not a trigger — fixed.", this);
            }

            if (col is SphereCollider sphere && sphere.radius < 0.5f)
                Debug.LogWarning($"[ProximityInteractor] '{gameObject.name}' trigger sphere radius is " +
                                 $"{sphere.radius:F3} m — very small. Set it to at least 1.0 m.", this);
        }

        private void OnDisable()
        {
            if (_playerInRange)
                ExitProximity();
        }

        // ── Trigger Callbacks ─────────────────────────────────────────────────
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(_playerTag)) return;
            if (_playerInRange) return;

            _playerInRange = true;

            // Only show prompt and subscribe to input if this IS the current active task.
            if (!IsActiveTask())
            {
                Debug.Log($"[ProximityInteractor] '{gameObject.name}': player entered but task " +
                          $"'{_taskId}' is not active — prompt suppressed.");
                return;
            }

            SubscribeToInput();
            OnPlayerEntered?.Invoke(_interactionPrompt);
            Debug.Log($"[ProximityInteractor] '{gameObject.name}': player entered. Prompt shown.");
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(_playerTag)) return;
            if (!_playerInRange) return;

            ExitProximity();
            Debug.Log($"[ProximityInteractor] '{gameObject.name}': player exited. Prompt hidden.");
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Immediately hide the prompt without requiring the player to leave.
        /// Called by EquipmentInteraction after successful activation.
        /// </summary>
        public void ForceHidePrompt()
        {
            UnsubscribeFromInput();
            OnPlayerExited?.Invoke();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true only if _taskId matches the currently active task AND it has not been completed.
        /// </summary>
        private bool IsActiveTask()
        {
            if (string.IsNullOrWhiteSpace(_taskId)) return false;
            var mgr = TaskProgressionManager.Instance;
            if (mgr == null) return false;
            return mgr.IsCurrentTask(_taskId.Trim());
        }

        private void ExitProximity()
        {
            _playerInRange = false;
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
            Debug.Log($"[ProximityInteractor] '{gameObject.name}': Use received. " +
                      $"playerInRange={_playerInRange}, isActiveTask={IsActiveTask()}");

            // Double-guard: re-check task state at the moment Use is pressed.
            if (!_playerInRange) return;
            if (!IsActiveTask())
            {
                Debug.Log($"[ProximityInteractor] '{gameObject.name}': Use ignored — task not active.");
                return;
            }

            OnInteractionTriggered?.Invoke();
        }
    }
}
