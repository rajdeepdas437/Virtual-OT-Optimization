using UnityEngine;
using Autohand;
using SmartOT.Core;

namespace SmartOT.Equipment
{
    /// <summary>
    /// Veress Needle attachment interaction.
    ///
    /// Workflow:
    ///   1. Player grabs the Veress needle (AutoHand handles grabbing normally).
    ///   2. Player brings it to the abdominal insertion PlacePoint.
    ///   3. AutoHand's PlacePoint auto-places it when it enters placeRadius.
    ///   4. PlacePoint fires OnPlaceEvent → CompleteTask("Insert Veress Needle").
    ///
    /// Inspector setup (on the Veress Needle GameObject):
    ///   - Requires Grabbable + Rigidbody (AutoHand).
    ///   - _attachmentPoint → drag the PlacePoint GO (e.g. VeressNeedle_InsertionPoint).
    ///   - _taskId          → "Insert Veress Needle" (default).
    ///   - Also add GrabbablePhysicsRestorer for correct physics after release.
    ///   - On the PlacePoint: placeRadius = 0.2–0.4, makePlacedKinematic = true,
    ///     disableGrabOnPlace = true (needle stays inserted).
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("SmartOT/Veress Needle Attachment")]
    public class VeressNeedleAttachment : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Task Linkage")]
        [Tooltip("Must exactly match the TaskData.taskName.")]
        [SerializeField] private string _taskId = "Insert Veress Needle";

        [Header("References")]
        [Tooltip("PlacePoint at the abdominal insertion location.")]
        [SerializeField] private PlacePoint _attachmentPoint;

        // ── State ─────────────────────────────────────────────────────────────
        private bool _taskCompleted = false;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            if (_attachmentPoint == null)
                Debug.LogError("[VeressNeedleAttachment] _attachmentPoint not assigned. " +
                               "Drag the insertion PlacePoint into the Inspector.", this);
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
        /// We confirm it is this needle before completing the task.
        /// </summary>
        private void OnPlaced(PlacePoint point, Grabbable placed)
        {
            if (_taskCompleted) return;

            // Confirm the placed object is this needle.
            if (placed == null || placed.gameObject != gameObject)
            {
                Debug.Log("[VeressNeedleAttachment] OnPlaced fired but placed object " +
                          $"is '{placed?.name}', not this needle — ignoring.");
                return;
            }

            var mgr = TaskProgressionManager.Instance;
            if (mgr == null)
            {
                Debug.LogError("[VeressNeedleAttachment] TaskProgressionManager not found.", this);
                return;
            }

            if (!mgr.IsCurrentTask(_taskId.Trim()))
            {
                Debug.Log($"[VeressNeedleAttachment] Placed but '{_taskId}' is not the " +
                          $"current task (current='{mgr.CurrentTask?.taskName}'). Ignoring.");
                return;
            }

            _taskCompleted = true;
            Debug.Log("[VeressNeedleAttachment] Veress needle inserted — completing task.");
            mgr.CompleteTask(_taskId.Trim());
        }
    }
}
