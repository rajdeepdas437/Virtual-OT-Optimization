using UnityEngine;
using Autohand;
using SmartOT.Core;

namespace SmartOT.Equipment
{
    /// <summary>
    /// CO2 Tube connection manager.
    ///
    /// Place this component on ONE neutral GameObject (e.g. the CO2 insufflator,
    /// or a dedicated "CO2_Setup" empty GO). It monitors two PlacePoints:
    ///   • _needlePlacePoint      — connection at the Veress needle
    ///   • _insufflatorPlacePoint — connection at the CO2 insufflator (one end only)
    ///
    /// Task completes when BOTH PlacePoints have an object placed on them.
    /// The tube-end GameObjects themselves only need Grabbable + Rigidbody +
    /// GrabbablePhysicsRestorer — no extra SmartOT script required on them.
    ///
    /// Inspector setup (on the CO2_Setup / insufflator GameObject):
    ///   - _taskId                → "Connect CO2 Tube" (must match TaskData.taskName).
    ///   - _needlePlacePoint      → PlacePoint GO at the Veress needle fitting.
    ///   - _insufflatorPlacePoint → PlacePoint GO at the CO2 insufflator port.
    ///
    /// PlacePoint settings for each connection point:
    ///   - Place Radius          = 0.3
    ///   - Make Placed Kinematic = true
    ///   - Disable Grab On Place = true  (tube end stays locked in position)
    ///   - Parent On Place       = true
    /// </summary>
    [AddComponentMenu("SmartOT/CO2 Tube Connection")]
    public class CO2TubeConnection : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Task Linkage")]
        [Tooltip("Must exactly match the TaskData.taskName.")]
        [SerializeField] private string _taskId = "Connect CO2 Tube";

        [Header("Connection Points")]
        [Tooltip("PlacePoint at the Veress needle tube fitting.")]
        [SerializeField] private PlacePoint _needlePlacePoint;

        [Tooltip("PlacePoint at the CO2 insufflator tube port.")]
        [SerializeField] private PlacePoint _insufflatorPlacePoint;

        // ── State ─────────────────────────────────────────────────────────────
        private bool _needleEndConnected      = false;
        private bool _insufflatorEndConnected = false;
        private bool _taskCompleted           = false;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            if (_needlePlacePoint == null)
                Debug.LogError("[CO2TubeConnection] _needlePlacePoint not assigned. " +
                               "Drag the needle-side PlacePoint into the Inspector.", this);

            if (_insufflatorPlacePoint == null)
                Debug.LogError("[CO2TubeConnection] _insufflatorPlacePoint not assigned. " +
                               "Drag the insufflator-side PlacePoint into the Inspector.", this);
        }

        private void OnEnable()
        {
            if (_needlePlacePoint != null)
                _needlePlacePoint.OnPlaceEvent += OnNeedleEndPlaced;

            if (_insufflatorPlacePoint != null)
                _insufflatorPlacePoint.OnPlaceEvent += OnInsufflatorEndPlaced;
        }

        private void OnDisable()
        {
            if (_needlePlacePoint != null)
                _needlePlacePoint.OnPlaceEvent -= OnNeedleEndPlaced;

            if (_insufflatorPlacePoint != null)
                _insufflatorPlacePoint.OnPlaceEvent -= OnInsufflatorEndPlaced;
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnNeedleEndPlaced(PlacePoint point, Grabbable placed)
        {
            if (_needleEndConnected || _taskCompleted) return;

            if (!IsActiveTask()) return;

            _needleEndConnected = true;
            Debug.Log($"[CO2TubeConnection] Needle end connected ('{placed?.name}'). " +
                      $"Insufflator end connected: {_insufflatorEndConnected}.");
            TryCompleteTask();
        }

        private void OnInsufflatorEndPlaced(PlacePoint point, Grabbable placed)
        {
            if (_insufflatorEndConnected || _taskCompleted) return;

            if (!IsActiveTask()) return;

            _insufflatorEndConnected = true;
            Debug.Log($"[CO2TubeConnection] Insufflator end connected ('{placed?.name}'). " +
                      $"Needle end connected: {_needleEndConnected}.");
            TryCompleteTask();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void TryCompleteTask()
        {
            if (!_needleEndConnected || !_insufflatorEndConnected) return;

            var mgr = TaskProgressionManager.Instance;
            if (mgr == null)
            {
                Debug.LogError("[CO2TubeConnection] TaskProgressionManager not found.", this);
                return;
            }

            _taskCompleted = true;
            Debug.Log("[CO2TubeConnection] Both CO2 tube ends connected — completing task.");
            mgr.CompleteTask(_taskId.Trim());
        }

        private bool IsActiveTask()
        {
            var mgr = TaskProgressionManager.Instance;
            if (mgr == null) return false;

            if (!mgr.IsCurrentTask(_taskId.Trim()))
            {
                Debug.Log($"[CO2TubeConnection] PlaceEvent received but '{_taskId}' is not " +
                          $"the current task (current='{mgr.CurrentTask?.taskName}'). Ignoring.");
                return false;
            }
            return true;
        }
    }
}
