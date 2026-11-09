using UnityEngine;
using UnityEngine.Events;
using TMPro;
using SmartOT.Core;
using SmartOT.Input;

namespace SmartOT.UI
{
    /// <summary>
    /// Tracks simulation elapsed time and shows the results menu when all tasks complete.
    ///
    /// ── How to use ────────────────────────────────────────────────────────────
    /// 1. Create a World Space Canvas named "ResultsMenu" in the scene.
    ///    • Add a TrackedDeviceGraphicRaycaster component (from XR Interaction Toolkit)
    ///      so Quest 2 controller rays can hit the buttons.
    ///    • Keep the Canvas INACTIVE in the Hierarchy (tick off) — this script activates it.
    ///    • Add the following child TMP_Text and Button objects (names are your choice):
    ///        - A TMP_Text for the arrangement label  → drag into _arrangementLabelText
    ///        - A TMP_Text for the elapsed time       → drag into _elapsedTimeText
    ///        - Three UI Buttons, each with a TMP_Text child label
    ///          Wire each Button's OnClick() to this component's public methods:
    ///            Button 1 → OnSelectArrangement1()
    ///            Button 2 → OnSelectArrangement2()
    ///            Button 3 → OnSelectArrangement3()
    ///
    /// 2. Add this component to any persistent GO in the scene (e.g. SmartOT_TaskManager).
    ///
    /// 3. In the Inspector, assign:
    ///    _resultsCanvasGO      → your "ResultsMenu" Canvas GameObject
    ///    _arrangementLabelText → TMP_Text that shows "Time taken for arrangement N"
    ///    _elapsedTimeText      → TMP_Text that shows "MM:SS"
    ///    _inputHandler         → SmartOTInputHandler (disabled on completion so Use stops)
    ///    _taskHUD              → Main UI Canvas GameObject (hidden on completion)
    ///    _arrangementNumber    → 1, 2, or 3 (change per scene)
    ///    _onArrangement1/2/3Selected → optional UnityEvents for future scene loading
    ///
    /// ── What it does ──────────────────────────────────────────────────────────
    /// • Records Time.time in Start() — this is the simulation start time.
    /// • When TaskProgressionManager.OnAllTasksCompleted fires:
    ///     - Stops and stores elapsed time.
    ///     - Disables SmartOTInputHandler so no more Use callbacks fire.
    ///     - Hides the task HUD Canvas.
    ///     - Populates the two text fields.
    ///     - Positions the results canvas 2 m in front of the player camera.
    ///     - Activates the results canvas.
    /// • Button callbacks log the selection and invoke the matching UnityEvent.
    /// </summary>
    [AddComponentMenu("SmartOT/Simulation Results Menu")]
    public class SimulationResultsMenu : MonoBehaviour
    {
        // ── Inspector: Canvas ─────────────────────────────────────────────────
        [Header("Results Canvas (you create this manually)")]
        [Tooltip("Drag your 'ResultsMenu' Canvas GameObject here. Must start INACTIVE.")]
        [SerializeField] private GameObject _resultsCanvasGO;

        [Tooltip("TMP_Text that will show 'Time taken for arrangement N'. Can be null.")]
        [SerializeField] private TMP_Text _arrangementLabelText;

        [Tooltip("TMP_Text that will show elapsed time in MM:SS format.")]
        [SerializeField] private TMP_Text _elapsedTimeText;

        // ── Inspector: OT systems to disable on completion ────────────────────
        [Header("OT Systems (disabled when results menu appears)")]
        [Tooltip("Drag SmartOTInputHandler here. It is disabled so Use interactions stop.")]
        [SerializeField] private SmartOTInputHandler _inputHandler;

        [Tooltip("Drag the task HUD Canvas GameObject (e.g. 'Main UI') here. It is hidden on completion.")]
        [SerializeField] private GameObject _taskHUD;

        // ── Inspector: Configuration ──────────────────────────────────────────
        [Header("Configuration")]
        [Tooltip("Which arrangement this scene represents. Shown in the results label.")]
        [SerializeField] private int _arrangementNumber = 1;

        [Tooltip("Distance in metres to place the results canvas in front of the player camera.")]
        [SerializeField] private float _menuDistance = 2f;

        // ── Inspector: Selection Events ───────────────────────────────────────
        [Header("Arrangement Selection (wire future scene loads here)")]
        [Tooltip("Invoked when the player clicks Arrangement 1.")]
        [SerializeField] private UnityEvent _onArrangement1Selected = new UnityEvent();

        [Tooltip("Invoked when the player clicks Arrangement 2.")]
        [SerializeField] private UnityEvent _onArrangement2Selected = new UnityEvent();

        [Tooltip("Invoked when the player clicks Arrangement 3.")]
        [SerializeField] private UnityEvent _onArrangement3Selected = new UnityEvent();

        // ── State ─────────────────────────────────────────────────────────────
        private float _startTime;
        private float _elapsedSeconds;
        private bool  _menuShown;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            // Record simulation start time silently (no display).
            _startTime = Time.time;
            Debug.Log($"[SimulationResultsMenu] Timer started at t={_startTime:F2}s.");

            // Validate canvas reference.
            if (_resultsCanvasGO == null)
            {
                Debug.LogWarning("[SimulationResultsMenu] _resultsCanvasGO is not assigned. " +
                                 "The results menu will not appear on completion. " +
                                 "Drag your 'ResultsMenu' Canvas GO into the Inspector field.", this);
            }
            else
            {
                // Ensure it starts hidden regardless of editor state.
                _resultsCanvasGO.SetActive(false);
            }

            // Subscribe to the completion event.
            var mgr = TaskProgressionManager.Instance;
            if (mgr != null)
            {
                mgr.OnAllTasksCompleted += HandleAllTasksCompleted;
                Debug.Log("[SimulationResultsMenu] Subscribed to TaskProgressionManager.OnAllTasksCompleted.");
            }
            else
            {
                Debug.LogError("[SimulationResultsMenu] TaskProgressionManager.Instance is null in Start(). " +
                               "Ensure SmartOT_TaskManager is in the scene.", this);
            }
        }

        private void OnDestroy()
        {
            var mgr = TaskProgressionManager.Instance;
            if (mgr != null)
                mgr.OnAllTasksCompleted -= HandleAllTasksCompleted;
        }

        // ── Completion Handler ────────────────────────────────────────────────

        private void HandleAllTasksCompleted()
        {
            if (_menuShown) return; // guard against accidental double-fire
            _menuShown = true;

            // Stop timer.
            _elapsedSeconds = Time.time - _startTime;
            Debug.Log($"[SimulationResultsMenu] All tasks completed. " +
                      $"Elapsed time: {FormatTime(_elapsedSeconds)} ({_elapsedSeconds:F1}s).");

            DisableOTSystems();
            ShowResultsMenu();
        }

        private void DisableOTSystems()
        {
            // Stop Use interactions so the player can't trigger tasks after the menu appears.
            if (_inputHandler != null)
            {
                _inputHandler.enabled = false;
                Debug.Log("[SimulationResultsMenu] SmartOTInputHandler disabled.");
            }
            else
            {
                Debug.LogWarning("[SimulationResultsMenu] _inputHandler not assigned — " +
                                 "Use interactions will remain active.", this);
            }

            // Hide the task HUD so only the results menu is visible.
            if (_taskHUD != null)
            {
                _taskHUD.SetActive(false);
                Debug.Log("[SimulationResultsMenu] Task HUD hidden.");
            }
            else
            {
                Debug.LogWarning("[SimulationResultsMenu] _taskHUD not assigned — " +
                                 "task HUD will remain visible alongside the results menu.", this);
            }
        }

        private void ShowResultsMenu()
        {
            if (_resultsCanvasGO == null)
            {
                Debug.LogError("[SimulationResultsMenu] Cannot show results — " +
                               "_resultsCanvasGO is null. Assign it in the Inspector.", this);
                return;
            }

            // Populate text fields before activating so they are correct on first frame.
            if (_arrangementLabelText != null)
                _arrangementLabelText.text = $"Time taken for arrangement {_arrangementNumber}";

            if (_elapsedTimeText != null)
                _elapsedTimeText.text = FormatTime(_elapsedSeconds);

            // Position the canvas in front of the player before making it visible.
            PositionCanvasInFrontOfCamera();

            _resultsCanvasGO.SetActive(true);
            Debug.Log("[SimulationResultsMenu] Results canvas activated.");
        }

        private void PositionCanvasInFrontOfCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[SimulationResultsMenu] No Camera tagged 'MainCamera' found. " +
                                 "ResultsMenu will appear at its current world position.", this);
                return;
            }

            Transform camT = cam.transform;

            // Project camera forward onto the horizontal plane so the menu sits upright.
            Vector3 forward = camT.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward; // fallback if looking straight up/down
            forward.Normalize();

            // Place menu at eye height, _menuDistance in front of the camera.
            Vector3 position = camT.position + forward * _menuDistance;
            position.y = camT.position.y;

            _resultsCanvasGO.transform.position = position;

            // Face the camera (canvas normal points toward the player).
            Vector3 toCamera = camT.position - position;
            if (toCamera.sqrMagnitude > 0.001f)
                _resultsCanvasGO.transform.rotation =
                    Quaternion.LookRotation(-toCamera.normalized, Vector3.up);

            Debug.Log($"[SimulationResultsMenu] Results canvas positioned at {position}.");
        }

        // ── Button Callbacks (wire to your three Buttons' OnClick()) ──────────

        /// <summary>
        /// Wire this to Arrangement 1 button's OnClick() in the Inspector.
        /// </summary>
        public void OnSelectArrangement1()
        {
            Debug.Log("[SimulationResultsMenu] Player selected Arrangement 1.");
            _onArrangement1Selected?.Invoke();
        }

        /// <summary>
        /// Wire this to Arrangement 2 button's OnClick() in the Inspector.
        /// </summary>
        public void OnSelectArrangement2()
        {
            Debug.Log("[SimulationResultsMenu] Player selected Arrangement 2.");
            _onArrangement2Selected?.Invoke();
        }

        /// <summary>
        /// Wire this to Arrangement 3 button's OnClick() in the Inspector.
        /// </summary>
        public void OnSelectArrangement3()
        {
            Debug.Log("[SimulationResultsMenu] Player selected Arrangement 3.");
            _onArrangement3Selected?.Invoke();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Returns elapsed seconds as a "MM:SS" string.</summary>
        private static string FormatTime(float totalSeconds)
        {
            int minutes = Mathf.FloorToInt(totalSeconds / 60f);
            int seconds  = Mathf.FloorToInt(totalSeconds % 60f);
            return $"{minutes:D2}:{seconds:D2}";
        }

        // ── Editor Gizmo ─────────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_arrangementNumber < 1) _arrangementNumber = 1;
            if (_menuDistance      < 0.5f) _menuDistance   = 0.5f;
        }
#endif
    }
}
