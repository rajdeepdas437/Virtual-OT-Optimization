using UnityEngine;
using Autohand;
using SmartOT.Core;

namespace SmartOT.Equipment
{
    /// <summary>
    /// IV Tube attachment interaction.
    ///
    /// Workflow:
    ///   1. Player grabs the IV tube (AutoHand handles grabbing normally).
    ///   2. Player brings the tube close to the IV attachment PlacePoint.
    ///   3. AutoHand's PlacePoint auto-places the tube when it enters placeRadius
    ///      (either on release or via PlacePoint's internal CheckPlaceObject coroutine).
    ///   4. PlacePoint fires OnPlaceEvent → we call CompleteTask("Attach IV Tube").
    ///
    /// Fix log (v2):
    ///   Replaced OnReleaseEvent approach with PlacePoint.OnPlaceEvent subscription.
    ///   The old approach used CanPlace() AFTER auto-place already ran, which returned
    ///   false (placedObject != null), so CompleteTask was never called.
    ///
    /// Inspector setup (on the IV Tube GameObject):
    ///   - Requires Grabbable + Rigidbody (AutoHand).
    ///   - _ivAttachmentPoint → drag the PlacePoint GO (e.g. IV_AttachmentPoint).
    ///   - _taskId            → "Attach IV Tube" (default).
    ///   - Also add GrabbablePhysicsRestorer to this GO for correct physics after release.
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("SmartOT/IV Attachment Interaction")]
    public class IVAttachmentInteraction : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Task Linkage")]
        [Tooltip("Must exactly match the TaskData.taskName.")]
        [SerializeField] private string _taskId = "Attach IV Tube";

        [Header("References")]
        [Tooltip("PlacePoint at the patient's IV attachment location.")]
        [SerializeField] private PlacePoint _ivAttachmentPoint;

        // ── State ─────────────────────────────────────────────────────────────
        private bool _taskCompleted = false;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            if (_ivAttachmentPoint == null)
                Debug.LogError("[IVAttachmentInteraction] _ivAttachmentPoint not assigned. " +
                               "Drag the IV PlacePoint into the Inspector.", this);
        }

        private void OnEnable()
        {
            if (_ivAttachmentPoint != null)
                _ivAttachmentPoint.OnPlaceEvent += OnPlaced;
        }

        private void OnDisable()
        {
            if (_ivAttachmentPoint != null)
                _ivAttachmentPoint.OnPlaceEvent -= OnPlaced;
        }

        // ── PlacePoint Event Handler ──────────────────────────────────────────
        /// <summary>
        /// Called by AutoHand's PlacePoint whenever ANY Grabbable is placed on it.
        /// We confirm it is this IV tube before completing the task.
        /// </summary>
        private void OnPlaced(PlacePoint point, Grabbable placed)
        {
            if (_taskCompleted) return;

            // Confirm the placed object is this IV tube, not something else.
            if (placed == null || placed.gameObject != gameObject)
            {
                Debug.Log("[IVAttachmentInteraction] OnPlaced fired but placed object " +
                          $"is '{placed?.name}', not this tube — ignoring.");
                return;
            }

            // Guard: only complete if "Attach IV Tube" is the current active task.
            var mgr = TaskProgressionManager.Instance;
            if (mgr == null)
            {
                Debug.LogError("[IVAttachmentInteraction] TaskProgressionManager not found.", this);
                return;
            }

            if (!mgr.IsCurrentTask(_taskId.Trim()))
            {
                Debug.Log($"[IVAttachmentInteraction] Placed but '{_taskId}' is not the " +
                          $"current task (current='{mgr.CurrentTask?.taskName}'). Ignoring.");
                return;
            }

            _taskCompleted = true;
            Debug.Log("[IVAttachmentInteraction] IV tube attached — completing task.");
            mgr.CompleteTask(_taskId.Trim());
        }
    }
}
