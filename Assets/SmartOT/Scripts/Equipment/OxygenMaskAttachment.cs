using UnityEngine;
using Autohand;
using SmartOT.Core;

namespace SmartOT.Equipment
{
    /// <summary>
    /// Oxygen Mask attachment interaction.
    ///
    /// Workflow:
    ///   1. Player grabs the Oxygen Mask (AutoHand handles grabbing normally).
    ///   2. Player brings the mask close to the OxygenMask_AttachmentPoint PlacePoint.
    ///   3. AutoHand's PlacePoint auto-places the mask when it enters placeRadius.
    ///   4. PlacePoint fires OnPlaceEvent → we call CompleteTask("Attach Oxygen Mask").
    ///
    /// Inspector setup (on the Oxygen Mask GameObject):
    ///   - Requires Grabbable + Rigidbody (AutoHand).
    ///   - _attachmentPoint → drag the PlacePoint GO (e.g. OxygenMask_AttachmentPoint).
    ///   - _taskId          → "Attach Oxygen Mask" (default).
    ///   - Also add GrabbablePhysicsRestorer to this GO for correct physics after release.
    ///   - On the PlacePoint: set placeRadius = 0.3–0.5, makePlacedKinematic = true,
    ///     disableGrabOnPlace = true (prevents accidental detachment during gameplay).
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("SmartOT/Oxygen Mask Attachment")]
    public class OxygenMaskAttachment : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Task Linkage")]
        [Tooltip("Must exactly match the TaskData.taskName.")]
        [SerializeField] private string _taskId = "Attach Oxygen Mask";

        [Header("References")]
        [Tooltip("PlacePoint at the patient's mouth / oxygen mask attachment location.")]
        [SerializeField] private PlacePoint _attachmentPoint;

        // ── State ─────────────────────────────────────────────────────────────
        private bool _taskCompleted = false;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            if (_attachmentPoint == null)
                Debug.LogError("[OxygenMaskAttachment] _attachmentPoint not assigned. " +
                               "Drag the OxygenMask PlacePoint into the Inspector.", this);
        }

        private void OnEnable()
        {
            if (_attachmentPoint != null)
                _attachmentPoint.OnPlaceEvent += OnPlaced;
        }

        private void OnDisable()
        {
            if (_attachmentPoint != null)
                _attachmentPoint.OnPlaceEvent -= OnPlaced;
        }

        // ── PlacePoint Event Handler ──────────────────────────────────────────
        /// <summary>
        /// Called by AutoHand's PlacePoint whenever ANY Grabbable is placed on it.
        /// We confirm it is this oxygen mask before completing the task.
        /// </summary>
        private void OnPlaced(PlacePoint point, Grabbable placed)
        {
            if (_taskCompleted) return;

            // Confirm the placed object is this oxygen mask, not something else.
            if (placed == null || placed.gameObject != gameObject)
            {
                Debug.Log("[OxygenMaskAttachment] OnPlaced fired but placed object " +
                          $"is '{placed?.name}', not this mask — ignoring.");
                return;
            }

            // Guard: only complete if "Attach Oxygen Mask" is the current active task.
            var mgr = TaskProgressionManager.Instance;
            if (mgr == null)
            {
                Debug.LogError("[OxygenMaskAttachment] TaskProgressionManager not found.", this);
                return;
            }

            if (!mgr.IsCurrentTask(_taskId.Trim()))
            {
                Debug.Log($"[OxygenMaskAttachment] Placed but '{_taskId}' is not the " +
                          $"current task (current='{mgr.CurrentTask?.taskName}'). Ignoring.");
                return;
            }

            _taskCompleted = true;
            Debug.Log("[OxygenMaskAttachment] Oxygen mask attached — completing task.");
            mgr.CompleteTask(_taskId.Trim());
        }
    }
}
