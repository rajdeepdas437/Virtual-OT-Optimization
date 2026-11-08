using UnityEngine;
using Autohand;
using SmartOT.Core;

namespace SmartOT.Equipment
{
    /// <summary>
    /// Trocar placement manager.
    ///
    /// Place this component on ONE neutral GameObject (e.g. an empty "TrocarSetup" GO).
    /// It monitors up to 4 PlacePoints on the patient's abdomen. The task completes
    /// when all assigned PlacePoints have a trocar placed on them.
    ///
    /// Clinical workflow:
    ///   1. Player grabs a trocar (AutoHand handles grabbing).
    ///   2. Player brings it to one of the abdominal insertion sites.
    ///   3. AutoHand's PlacePoint auto-snaps it into position.
    ///   4. Repeat for each trocar.
    ///   5. When all PlacePoints are filled → CompleteTask("Place Trocars").
    ///
    /// Inspector setup (on the TrocarSetup GameObject):
    ///   - _taskId            → "Place Trocars" (must match TaskData.taskName).
    ///   - _trocarPlacePoints → assign all trocar PlacePoints (1–4 entries).
    ///
    /// For each trocar PlacePoint GO:
    ///   - Add PlacePoint component.
    ///   - Place Radius = 0.3, Make Placed Kinematic = true,
    ///     Disable Grab On Place = true, Parent On Place = true.
    ///
    /// For each trocar Grabbable GO:
    ///   - Confirm Grabbable + Rigidbody present.
    ///   - Add GrabbablePhysicsRestorer.
    ///   - No additional SmartOT script needed on the trocar itself.
    /// </summary>
    [AddComponentMenu("SmartOT/Trocar Placement Manager")]
    public class TrocarPlacementManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Task Linkage")]
        [Tooltip("Must exactly match the TaskData.taskName.")]
        [SerializeField] private string _taskId = "Take trocar from table and place it on left side of abdomen";

        [Header("Trocar Placement Points")]
        [Tooltip("All PlacePoints where trocars must be placed. " +
                 "Task completes when every point here has an object placed on it.")]
        [SerializeField] private PlacePoint[] _trocarPlacePoints;

        // ── State ─────────────────────────────────────────────────────────────
        private bool[] _placed;
        private bool   _taskCompleted = false;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            if (_trocarPlacePoints == null || _trocarPlacePoints.Length == 0)
            {
                Debug.LogError("[TrocarPlacementManager] No PlacePoints assigned. " +
                               "Drag the trocar PlacePoints into _trocarPlacePoints.", this);
                return;
            }

            _placed = new bool[_trocarPlacePoints.Length];

            for (int i = 0; i < _trocarPlacePoints.Length; i++)
            {
                if (_trocarPlacePoints[i] == null)
                    Debug.LogError($"[TrocarPlacementManager] PlacePoint slot [{i}] is null.", this);
            }
        }

        private void OnEnable()
        {
            if (_trocarPlacePoints == null) return;
            for (int i = 0; i < _trocarPlacePoints.Length; i++)
            {
                if (_trocarPlacePoints[i] != null)
                {
                    int capturedIndex = i; // capture for lambda closure
                    _trocarPlacePoints[i].OnPlaceEvent +=
                        (point, grabbable) => OnTrocarPlaced(capturedIndex, point, grabbable);
                }
            }
        }

        // Note: OnPlaceEvent uses delegates; we cannot unsubscribe the lambdas by reference.
        // For this simulation the GO persists for the scene lifetime, so this is acceptable.
        // If you need clean unsubscription, store the delegates as named methods instead.

        // ── Event Handler ─────────────────────────────────────────────────────
        private void OnTrocarPlaced(int slotIndex, PlacePoint point, Grabbable placed)
        {
            if (_taskCompleted) return;
            if (_placed != null && _placed[slotIndex]) return; // already counted this slot

            // Task-state guard
            var mgr = TaskProgressionManager.Instance;
            if (mgr == null)
            {
                Debug.LogError("[TrocarPlacementManager] TaskProgressionManager not found.", this);
                return;
            }

            if (!mgr.IsCurrentTask(_taskId.Trim()))
            {
                Debug.Log($"[TrocarPlacementManager] Trocar placed on slot {slotIndex} " +
                          $"but '{_taskId}' is not the current task " +
                          $"(current='{mgr.CurrentTask?.taskName}'). Ignoring.");
                return;
            }

            _placed[slotIndex] = true;
            int placedCount = CountPlaced();
            Debug.Log($"[TrocarPlacementManager] Trocar placed on slot {slotIndex} " +
                      $"('{placed?.name}'). {placedCount}/{_trocarPlacePoints.Length} placed.");

            if (placedCount >= _trocarPlacePoints.Length)
            {
                _taskCompleted = true;
                Debug.Log("[TrocarPlacementManager] All trocars placed — completing task.");
                mgr.CompleteTask(_taskId.Trim());
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private int CountPlaced()
        {
            if (_placed == null) return 0;
            int count = 0;
            foreach (bool b in _placed) if (b) count++;
            return count;
        }
    }
}
