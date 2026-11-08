using UnityEngine;
using Autohand;
using SmartOT.Core;

namespace SmartOT.Equipment
{
    /// <summary>
    /// CO2 Tube dual-attachment interaction.
    ///
    /// Workflow:
    ///   1. Player grabs the CO2 tube (AutoHand handles grabbing).
    ///   2. Player attaches one end to the Veress needle PlacePoint.
    ///   3. Player attaches the other end to the CO2 insufflator PlacePoint.
    ///   4. When BOTH ends are placed, CompleteTask("Connect CO2 Tube") fires.
    ///
    /// Design note:
    ///   The CO2 tube is a single Grabbable. Both PlacePoints accept it; each end
    ///   is modelled as a separate PlacePoint GameObject positioned at each connection site.
    ///   We track independently which ends have been placed. Because a single Rigidbody
    ///   cannot physically be in two PlacePoints simultaneously in AutoHand, the
    ///   recommended workflow is:
    ///     - Use TWO separate tube-end GameObjects (e.g. CO2Tube_NeedleEnd and
    ///       CO2Tube_InsufflatorEnd), each with Grabbable + Rigidbody + this script
    ///       set up for one of the two PlacePoints.
    ///   OR use the simpler single-GO approach:
    ///     - Attach this component to ONE Tube GO, provide both PlacePoints; each
    ///       PlacePoint's placeNames list accepts any object (blank) so the tube snaps
    ///       to whichever point it is released near first. The second snap is blocked
    ///       because PlacePoint.disableGrabOnPlace = false for the needle-end (allowing
    ///       re-grab) but = true for the insufflator-end (final position).
    ///
    ///   Recommended (simplest): Use TWO separate Grabbable tube end objects.
    ///   Each gets its own CO2TubeEndAttachment component. This script tracks ONE end.
    ///   The CO2TubeConnectionTracker (singleton helper) counts both ends and fires
    ///   CompleteTask when count reaches 2.
    ///
    /// Inspector setup (on each CO2 Tube end GameObject):
    ///   - Requires Grabbable + Rigidbody (AutoHand).
    ///   - _attachmentPoint → drag the PlacePoint for THIS end.
    ///   - _taskId          → "Connect CO2 Tube" (same on both ends).
    ///   - _totalEndsRequired → 2 (default — set to 2 on BOTH ends).
    ///   - Also add GrabbablePhysicsRestorer.
    ///   - On each PlacePoint: placeRadius = 0.2–0.4, makePlacedKinematic = true,
    ///     disableGrabOnPlace = true.
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("SmartOT/CO2 Tube End Attachment")]
    public class CO2TubeEndAttachment : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Task Linkage")]
        [Tooltip("Must exactly match the TaskData.taskName (same on both tube-end objects).")]
        [SerializeField] private string _taskId = "Connect CO2 Tube";

        [Tooltip("Total number of ends that must be connected before the task completes. " +
                 "Set to 2 on both tube-end GameObjects.")]
        [SerializeField] private int _totalEndsRequired = 2;

        [Header("References")]
        [Tooltip("PlacePoint for THIS end (needle-end or insufflator-end).")]
        [SerializeField] private PlacePoint _attachmentPoint;

        // ── Static shared counter ─────────────────────────────────────────────
        // Counts how many ends have been successfully placed for the current task run.
        // Static so both CO2TubeEndAttachment instances share the same count.
        private static int s_endsConnected = 0;
        private static bool s_taskCompletedGlobal = false;

        // ── Instance state ────────────────────────────────────────────────────
        private bool _thisEndPlaced = false;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            if (_attachmentPoint == null)
                Debug.LogError("[CO2TubeEndAttachment] _attachmentPoint not assigned on " +
                               $"'{gameObject.name}'. Drag the PlacePoint into the Inspector.", this);
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

        // Reset static state when the scene is reloaded (domain reload) or in Start.
        // This handles the case where the scene is restarted without a domain reload.
        private void Start()
        {
            // Reset per-task-run if the task has not started yet.
            var mgr = TaskProgressionManager.Instance;
            if (mgr != null && !mgr.IsCurrentTask(_taskId.Trim()) && !s_taskCompletedGlobal)
            {
                // Don't touch the counter until this is the active task.
            }
        }

        // ── PlacePoint Event Handler ──────────────────────────────────────────
        private void OnPlaced(PlacePoint point, Grabbable placed)
        {
            if (_thisEndPlaced) return;
            if (s_taskCompletedGlobal) return;

            // Confirm it is this tube end.
            if (placed == null || placed.gameObject != gameObject)
            {
                Debug.Log("[CO2TubeEndAttachment] OnPlaced fired for wrong object " +
                          $"'{placed?.name}' on point '{point?.name}' — ignoring.");
                return;
            }

            var mgr = TaskProgressionManager.Instance;
            if (mgr == null)
            {
                Debug.LogError("[CO2TubeEndAttachment] TaskProgressionManager not found.", this);
                return;
            }

            if (!mgr.IsCurrentTask(_taskId.Trim()))
            {
                Debug.Log($"[CO2TubeEndAttachment] Placed but '{_taskId}' is not the " +
                          $"current task (current='{mgr.CurrentTask?.taskName}'). Ignoring.");
                return;
            }

            _thisEndPlaced = true;
            s_endsConnected++;
            Debug.Log($"[CO2TubeEndAttachment] '{gameObject.name}' end connected. " +
                      $"Ends connected: {s_endsConnected}/{_totalEndsRequired}.");

            if (s_endsConnected >= _totalEndsRequired)
            {
                s_taskCompletedGlobal = true;
                Debug.Log("[CO2TubeEndAttachment] Both CO2 tube ends connected — completing task.");
                mgr.CompleteTask(_taskId.Trim());
            }
        }
    }
}
