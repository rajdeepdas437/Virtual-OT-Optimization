using UnityEngine;
using TMPro;
using SmartOT.Core;

namespace SmartOT.UI
{
    /// <summary>
    /// Displays SmartOT task information in the HUD.
    ///
    /// Fix log (v2):
    ///   - DefaultExecutionOrder(-5) so this Awake subscribes BEFORE TaskProgressionManager.Start
    ///     fires OnNextTaskStarted (fixes Bug B8 — UI missed the first event).
    ///   - ShowPrompt/HidePrompt now guard against null _promptText gracefully.
    ///   - All RefreshUI calls also refresh completedCount (was only done in HandleTaskCompleted).
    ///   - Added rich debug logging.
    ///   - _taskNameText now shows taskName (more reliable than taskDescription, which may be empty).
    ///     taskDescription is used for the subtitle line if present.
    ///
    /// Inspector assignments needed:
    ///   _phaseText, _taskNameText, _promptText, _completedCountText  — all TMP_Text on your Canvas.
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public class TaskUIManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("HUD Text Fields")]
        [Tooltip("Displays current phase. E.g. 'PHASE 1 – PATIENT PREPARATION'.")]
        [SerializeField] private TMP_Text _phaseText;

        [Tooltip("Displays current task name and description.")]
        [SerializeField] private TMP_Text _taskNameText;

        [Tooltip("Displays the interaction prompt when player is in range.")]
        [SerializeField] private TMP_Text _promptText;

        [Tooltip("Displays count of completed tasks out of total. E.g. 'Completed: 1/3'.")]
        [SerializeField] private TMP_Text _completedCountText;

        [Tooltip("Root panel to show/hide. Can be null.")]
        [SerializeField] private GameObject _hudPanel;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            // ── LOG 10 (early): Validate TMP references ───────────────────────
            Debug.Log($"[TaskUIManager] Awake. TMP refs — phase:{_phaseText != null}, " +
                      $"taskName:{_taskNameText != null}, prompt:{_promptText != null}, " +
                      $"count:{_completedCountText != null}");

            // Warn specifically about null references so they are easy to spot
            if (_phaseText         == null) Debug.LogWarning("[TaskUIManager] _phaseText is not assigned in Inspector!", this);
            if (_taskNameText      == null) Debug.LogWarning("[TaskUIManager] _taskNameText is not assigned in Inspector!", this);
            if (_promptText        == null) Debug.LogWarning("[TaskUIManager] _promptText is not assigned in Inspector!", this);
            if (_completedCountText == null) Debug.LogWarning("[TaskUIManager] _completedCountText is not assigned in Inspector!", this);
        }

        private void Start()
        {
            var mgr = TaskProgressionManager.Instance;
            if (mgr == null)
            {
                Debug.LogError("[TaskUIManager] TaskProgressionManager.Instance is null in Start(). " +
                               "Make sure SmartOT_TaskManager is in the scene and has the script attached.", this);
                return;
            }

            // Subscribe to progression events
            mgr.OnNextTaskStarted   += HandleNextTask;
            mgr.OnTaskCompleted     += HandleTaskCompleted;
            mgr.OnAllTasksCompleted += HandleAllDone;

            // Show initial state — the manager's Start() already fired (order guaranteed by
            // DefaultExecutionOrder), so we pull directly from the instance instead of waiting
            // for an event.
            RefreshUI(mgr.CurrentTask);
            HidePrompt();

            Debug.Log("[TaskUIManager] Start() complete — subscribed to TaskProgressionManager events " +
                      "and refreshed initial UI.");
        }

        private void OnDestroy()
        {
            var mgr = TaskProgressionManager.Instance;
            if (mgr == null) return;

            mgr.OnNextTaskStarted   -= HandleNextTask;
            mgr.OnTaskCompleted     -= HandleTaskCompleted;
            mgr.OnAllTasksCompleted -= HandleAllDone;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Shows the interaction prompt text.</summary>
        public void ShowPrompt(string promptText)
        {
            if (_promptText == null) return;
            _promptText.text = promptText;
            _promptText.gameObject.SetActive(true);
            Debug.Log($"[TaskUIManager] Prompt shown: '{promptText}'");
        }

        /// <summary>Hides and clears the interaction prompt.</summary>
        public void HidePrompt()
        {
            if (_promptText == null) return;
            _promptText.text = string.Empty;
            _promptText.gameObject.SetActive(false);
        }

        // ── Event Handlers ────────────────────────────────────────────────────
        private void HandleNextTask(TaskData task)
        {
            // ── LOG 10: UI refreshed with new task ────────────────────────────
            Debug.Log($"[TaskUIManager] HandleNextTask called — task='{task?.taskName}'");
            RefreshUI(task);
        }

        private void HandleTaskCompleted(int completedIndex)
        {
            HidePrompt();
            RefreshCompletedCount();
            Debug.Log($"[TaskUIManager] HandleTaskCompleted — index {completedIndex} done.");
        }

        private void HandleAllDone()
        {
            if (_taskNameText    != null) _taskNameText.text = "All Tasks Completed!";
            if (_phaseText       != null) _phaseText.text    = "PROCEDURE COMPLETE";
            RefreshCompletedCount();
            HidePrompt();
            Debug.Log("[TaskUIManager] HandleAllDone — UI updated to 'All Tasks Completed'.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Refreshes the phase and task-name text fields.
        /// Uses taskName as the primary display (taskDescription as subtitle if non-empty).
        /// </summary>
        private void RefreshUI(TaskData task)
        {
            if (task == null)
            {
                Debug.LogWarning("[TaskUIManager] RefreshUI called with null TaskData.");
                return;
            }

            if (_phaseText != null)
                _phaseText.text = $"PHASE {task.phase} – PATIENT PREPARATION";

            if (_taskNameText != null)
            {
                // Show taskName as primary line; description as secondary if available
                string desc = string.IsNullOrWhiteSpace(task.taskDescription)
                    ? string.Empty
                    : $"\n{task.taskDescription}";
                _taskNameText.text = $"Current Task: {task.taskName}{desc}";
            }

            RefreshCompletedCount();

            // ── LOG 10: UI refresh confirmed ──────────────────────────────────
            Debug.Log($"[TaskUIManager] UI refreshed — phase='{task.phase}', task='{task.taskName}'");
        }

        private void RefreshCompletedCount()
        {
            if (_completedCountText == null) return;
            var mgr = TaskProgressionManager.Instance;
            if (mgr == null) return;
            _completedCountText.text = $"Completed: {mgr.CompletedCount}/{mgr.TotalTaskCount}";
        }
    }
}
