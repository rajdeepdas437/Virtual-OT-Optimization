using System;
using UnityEngine;
using SmartOT.Core;

namespace SmartOT.Equipment
{
    /// <summary>
    /// Reusable component for any piece of equipment that can be activated via the Use interaction.
    ///
    /// Fix log (v2):
    ///   - After successful activation, calls ProximityInteractor.ForceHidePrompt() so the prompt
    ///     disappears immediately (fixes Bug B10 — prompt stayed visible after pressing E).
    ///   - Full debug logging added at every step (Logs 3-7).
    ///   - Trims _taskId before comparison to prevent whitespace mismatches.
    ///
    /// Inspector setup:
    ///   - _taskId must exactly match the TaskData.taskName in the ScriptableObject (after trim).
    ///   - _proximityInteractor — auto-found on same GameObject if not assigned.
    /// </summary>
    public class EquipmentInteraction : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Task Linkage")]
        [Tooltip("Must exactly match the TaskData.taskName for this equipment's task. " +
                 "E.g. 'Activate Anesthesia Machine' — no leading/trailing spaces.")]
        [SerializeField] private string _taskId;

        [Header("References")]
        [Tooltip("The ProximityInteractor on this object. Auto-found on same GameObject if empty.")]
        [SerializeField] private Interactions.ProximityInteractor _proximityInteractor;

        [Header("Visual State")]
        [Tooltip("GameObject to show when inactive (e.g. powered-off indicator). Can be null.")]
        [SerializeField] private GameObject _inactiveVisual;

        [Tooltip("GameObject to show when active (e.g. powered-on indicator / glow). Can be null.")]
        [SerializeField] private GameObject _activeVisual;

        [Header("Audio")]
        [Tooltip("Optional clip played on activation. Requires an AudioSource on this GameObject.")]
        [SerializeField] private AudioClip _activationSound;

        // ── Public Events ─────────────────────────────────────────────────────
        /// <summary>Raised when this equipment is successfully activated.</summary>
        public event Action OnEquipmentActivated;

        // ── State ─────────────────────────────────────────────────────────────
        private bool _isActivated = false;
        private AudioSource _audioSource;

        public bool IsActivated => _isActivated;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            // Auto-find ProximityInteractor on same GameObject if not assigned
            if (_proximityInteractor == null)
                _proximityInteractor = GetComponent<Interactions.ProximityInteractor>();

            if (_proximityInteractor == null)
                Debug.LogError($"[EquipmentInteraction] No ProximityInteractor on '{gameObject.name}'. " +
                               "Add one and ensure it's on the same GameObject.", this);

            if (string.IsNullOrWhiteSpace(_taskId))
                Debug.LogWarning($"[EquipmentInteraction] _taskId is empty on '{gameObject.name}'. " +
                                 "This equipment will never complete a task. Set it in the Inspector.", this);
            else
                Debug.Log($"[EquipmentInteraction] '{gameObject.name}' taskId='{_taskId.Trim()}'");

            _audioSource = GetComponent<AudioSource>();

            SetVisualState(false);
        }

        private void OnEnable()
        {
            if (_proximityInteractor != null)
                _proximityInteractor.OnInteractionTriggered += HandleInteraction;
        }

        private void OnDisable()
        {
            if (_proximityInteractor != null)
                _proximityInteractor.OnInteractionTriggered -= HandleInteraction;
        }

        // ── Interaction Handler ───────────────────────────────────────────────
        private void HandleInteraction()
        {
            // ── LOG 4: Interaction requested ──────────────────────────────────
            Debug.Log($"[EquipmentInteraction] '{gameObject.name}' HandleInteraction called. " +
                      $"taskId='{_taskId?.Trim()}', isActivated={_isActivated}");

            if (_isActivated)
            {
                Debug.Log($"[EquipmentInteraction] '{gameObject.name}' already activated — ignoring.");
                return;
            }

            if (TaskProgressionManager.Instance == null)
            {
                Debug.LogError("[EquipmentInteraction] TaskProgressionManager.Instance is null! " +
                               "Ensure SmartOT_TaskManager is in the scene.", this);
                return;
            }

            string trimmedId = _taskId?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(trimmedId) &&
                !TaskProgressionManager.Instance.IsCurrentTask(trimmedId))
            {
                Debug.Log($"[EquipmentInteraction] '{trimmedId}' is NOT the current task " +
                          $"(current='{TaskProgressionManager.Instance.CurrentTask?.taskName}'). " +
                          "Complete tasks in the correct order.");
                return;
            }

            // ── LOG 5: Equipment activation started ───────────────────────────
            Debug.Log($"[EquipmentInteraction] '{gameObject.name}' — activation starting.");
            Activate(trimmedId);
        }

        // ── Activation ────────────────────────────────────────────────────────
        private void Activate(string trimmedId)
        {
            _isActivated = true;

            SetVisualState(true);
            PlayActivationSound();

            // ── LOG 6: Equipment activation successful ────────────────────────
            Debug.Log($"[EquipmentInteraction] '{gameObject.name}' activated successfully.");

            // Hide prompt immediately — don't wait for trigger exit (fixes Bug B10)
            _proximityInteractor?.ForceHidePrompt();

            OnEquipmentActivated?.Invoke();

            // ── LOG 7: Task completion event sent ─────────────────────────────
            if (!string.IsNullOrEmpty(trimmedId))
            {
                Debug.Log($"[EquipmentInteraction] Calling CompleteTask('{trimmedId}').");
                TaskProgressionManager.Instance.CompleteTask(trimmedId);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void SetVisualState(bool activated)
        {
            if (_inactiveVisual != null) _inactiveVisual.SetActive(!activated);
            if (_activeVisual   != null) _activeVisual.SetActive(activated);
        }

        private void PlayActivationSound()
        {
            if (_audioSource != null && _activationSound != null)
                _audioSource.PlayOneShot(_activationSound);
        }
    }
}
