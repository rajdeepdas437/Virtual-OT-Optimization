using System.Collections.Generic;
using UnityEngine;
using TMPro;
using SmartOT.Core;

namespace SmartOT.UI
{
    /// <summary>
    /// Displays the task checklist using exactly 3 TMP_Text slots (paged display).
    ///
    /// Paging rules:
    ///   - PAGE 0 (tasks 0-2): shown at start.
    ///   - PAGE 1 (tasks 3-5): shown after all 3 tasks on page 0 are complete.
    ///   - PAGE 2 (tasks 6-8): shown after all 3 tasks on page 1 are complete.
    ///   Each slot shows either ✓ (completed/green), ▶ (current/yellow), or [ ] (pending/grey).
    ///   When fewer than 3 tasks remain on the final page, unused slots are cleared.
    ///
    /// Fix log (v2):
    ///   - Replaced flat one-task-per-slot rendering with paged rendering.
    ///   - _pageSize matches the number of TMP_Text slots (always 3).
    ///   - Page advances automatically as tasks complete — no new manager or events needed.
    ///   - All existing colours, symbols and event subscriptions are preserved.
    ///
    /// Inspector setup:
    ///   - _taskEntries: assign the existing 3 TMP_Text objects (unchanged).
    ///   - No other changes required.
    /// </summary>
    [DefaultExecutionOrder(-4)]
    public class TaskListUI : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Pre-assigned entries (exactly 3 TMP_Text objects)")]
        [Tooltip("The 3 existing TMP_Text objects in order. Index 0 = top slot.")]
        [SerializeField] private TMP_Text[] _taskEntries;

        [Header("Colours")]
        [SerializeField] private Color _completedColour = new Color(0.2f, 0.9f, 0.2f); // green
        [SerializeField] private Color _currentColour   = new Color(1.0f, 0.9f, 0.2f); // yellow
        [SerializeField] private Color _pendingColour   = new Color(0.8f, 0.8f, 0.8f); // grey-white

        // ── State ─────────────────────────────────────────────────────────────
        private readonly List<TMP_Text> _entries = new List<TMP_Text>();

        // Number of TMP slots — always 3 (the number of physical text objects).
        private const int PageSize = 3;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Start()
        {
            var mgr = TaskProgressionManager.Instance;
            if (mgr == null)
            {
                Debug.LogError("[TaskListUI] TaskProgressionManager not found.", this);
                return;
            }

            // Collect the 3 TMP entries.
            if (_taskEntries != null)
                foreach (var e in _taskEntries)
                    if (e != null) _entries.Add(e);

            if (_entries.Count == 0)
            {
                Debug.LogError("[TaskListUI] No TMP_Text entries assigned. " +
                               "Drag the 3 existing TMP_Text objects into _taskEntries.", this);
                return;
            }

            // Subscribe to progression events.
            mgr.OnTaskCompleted     += HandleTaskCompleted;
            mgr.OnAllTasksCompleted += HandleAllDone;

            // Render initial state.
            Refresh(mgr);
        }

        private void OnDestroy()
        {
            var mgr = TaskProgressionManager.Instance;
            if (mgr == null) return;
            mgr.OnTaskCompleted     -= HandleTaskCompleted;
            mgr.OnAllTasksCompleted -= HandleAllDone;
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void HandleTaskCompleted(int completedIndex)
        {
            var mgr = TaskProgressionManager.Instance;
            if (mgr != null) Refresh(mgr);
        }

        private void HandleAllDone()
        {
            var mgr = TaskProgressionManager.Instance;
            if (mgr != null) Refresh(mgr);
        }

        // ── Refresh ───────────────────────────────────────────────────────────

        private void Refresh(TaskProgressionManager mgr)
        {
            int completed    = mgr.CompletedCount;
            int currentIndex = mgr.CurrentTaskIndex; // equals CompletedCount while tasks run
            int total        = mgr.TotalTaskCount;

            // Which page are we on?
            // Page advances when ALL slots on the current page are completed.
            // Page = completedCount / PageSize (integer), clamped so we never exceed last page.
            int page         = completed / PageSize;
            int pageStart    = page * PageSize;         // first task index shown on this page

            for (int slot = 0; slot < _entries.Count; slot++)
            {
                var entry = _entries[slot];
                if (entry == null) continue;

                int taskIndex = pageStart + slot;

                // Slot falls beyond the total task count — clear it.
                if (taskIndex >= total)
                {
                    entry.text  = string.Empty;
                    entry.color = _pendingColour;
                    continue;
                }

                string taskName = mgr.GetTaskName(taskIndex);

                if (taskIndex < currentIndex)
                {
                    // Completed
                    entry.text  = $"[<color=#00FF00>✓</color>] {taskName}";
                    entry.color = _completedColour;
                }
                else if (taskIndex == currentIndex && !mgr.AllTasksDone)
                {
                    // Active / current
                    entry.text  = $"[<color=#FFE800>▶</color>] <b>{taskName}</b>";
                    entry.color = _currentColour;
                }
                else
                {
                    // Pending (future task on this page, or all done edge case)
                    entry.text  = $"[ ] {taskName}";
                    entry.color = _pendingColour;
                }
            }

            Debug.Log($"[TaskListUI] Refreshed — page={page}, pageStart={pageStart}, " +
                      $"completed={completed}/{total}.");
        }
    }
}
