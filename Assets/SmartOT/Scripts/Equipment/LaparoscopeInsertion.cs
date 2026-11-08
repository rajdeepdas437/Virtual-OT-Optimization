using UnityEngine;
using Autohand;
using SmartOT.Core;

namespace SmartOT.Equipment
{
    /// <summary>
    /// Laparoscope insertion interaction.
    ///
    /// Workflow:
    ///   1. Player grabs the laparoscope (AutoHand handles grabbing normally).
    ///   2. Player brings it to the laparoscope trocar port PlacePoint
    ///      (the existing Trocar_laparoscope GO in the PlacePoints group).
    ///   3. AutoHand's PlacePoint auto-places it when it enters placeRadius.
    ///   4. PlacePoint fires OnPlaceEvent → CompleteTask("Insert Laparoscope").
    ///
    /// Inspector setup (on the laparoscope GameObject):
    ///   - Requires Grabbable + Rigidbody (AutoHand) — already present in scene.
    ///   - _attachmentPoint → drag the Trocar_laparoscope PlacePoint GO.
    ///   - _taskId          → "Insert Laparoscope" (default).
    ///   - Add GrabbablePhysicsRestorer component.
    ///   - On the PlacePoint: placeRadius = 0.3–0.5, makePlacedKinematic = true,
    ///     disableGrabOnPlace = true (scope stays inserted).
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("SmartOT/Laparoscope Insertion")]
    public class LaparoscopeInsertion : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Task Linkage")]
        [Tooltip("Must exactly match the TaskData.taskName.")]
        [SerializeField] private string _taskId = "Take laparoscope from table and place it over the trocar";

        [Header("References")]
        [Tooltip("PlacePoint at the laparoscope trocar port (Trocar_laparoscope GO).")]
        [SerializeField] private PlacePoint _attachmentPoint;

        // ── State ─────────────────────────────────────────────────────────────
        private bool _taskCompleted = false;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            if (_attachmentPoint == null)
                Debug.LogError("[LaparoscopeInsertion] _attachmentPoint not assigned. " +
                               "Drag Trocar_laparoscope into the Inspector.", this);
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
        private void OnPlaced(PlacePoint point, Grabbable placed)
        {
            if (_taskCompleted) return;

            // Confirm it is this laparoscope.
            if (placed == null || placed.gameObject != gameObject)
            {
                Debug.Log("[LaparoscopeInsertion] OnPlaced fired for wrong object " +
                          $"'{placed?.name}' — ignoring.");
                return;
            }

            var mgr = TaskProgressionManager.Instance;
            if (mgr == null)
            {
                Debug.LogError("[LaparoscopeInsertion] TaskProgressionManager not found.", this);
                return;
            }

            if (!mgr.IsCurrentTask(_taskId.Trim()))
            {
                Debug.Log($"[LaparoscopeInsertion] Placed but '{_taskId}' is not the " +
                          $"current task (current='{mgr.CurrentTask?.taskName}'). Ignoring.");
                return;
            }

            _taskCompleted = true;
            Debug.Log("[LaparoscopeInsertion] Laparoscope inserted — completing task.");
            mgr.CompleteTask(_taskId.Trim());
        }
    }
}
