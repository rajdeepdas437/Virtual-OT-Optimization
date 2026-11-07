using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartOT.Core
{
    /// <summary>
    /// Central task-progression authority for SmartOT.
    ///
    /// Fix log (v2):
    ///   - Added DefaultExecutionOrder(-10) so Awake/Start fire before default-order scripts.
    ///     This guarantees TaskUIManager can subscribe in its own Awake before this Start fires
    ///     the initial OnNextTaskStarted event.
    ///   - Added rich debug logging at every stage.
    ///   - Trims whitespace from taskId comparisons to guard against accidental tab/space
    ///     characters in the ScriptableObject (Bug B4).
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class TaskProgressionManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        public static TaskProgressionManager Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Task Configuration")]
        [Tooltip("Assign the TaskSequence ScriptableObject that lists all tasks.")]
        [SerializeField] private TaskSequence _taskSequence;

        // ── Public Events ─────────────────────────────────────────────────────
        /// <summary>Raised when any task is completed. Arg: completed task index (0-based).</summary>
        public event Action<int> OnTaskCompleted;

        /// <summary>Raised after a task completes and a new one becomes active.</summary>
        public event Action<TaskData> OnNextTaskStarted;

        /// <summary>Raised when all tasks are done.</summary>
        public event Action OnAllTasksCompleted;

        // ── State ─────────────────────────────────────────────────────────────
        private int _currentTaskIndex = 0;
        private readonly HashSet<string> _completedTaskIds = new HashSet<string>();

        // ── Accessors ─────────────────────────────────────────────────────────
        public int CurrentTaskIndex  => _currentTaskIndex;
        public int TotalTaskCount    => _taskSequence != null ? _taskSequence.tasks.Length : 0;
        public int CompletedCount    => _completedTaskIds.Count;
        public bool AllTasksDone     => _currentTaskIndex >= TotalTaskCount;

        public TaskData CurrentTask
        {
            get
            {
                if (_taskSequence == null || AllTasksDone) return null;
                return _taskSequence.tasks[_currentTaskIndex];
            }
        }

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            // Simple non-persistent singleton
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[TaskProgressionManager] Duplicate instance found — destroying this one.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_taskSequence == null)
            {
                Debug.LogError("[TaskProgressionManager] TaskSequence is NOT assigned in the Inspector! " +
                               "Drag SmartOT_MainTaskSequence into the Task Sequence field.", this);
                return;
            }

            // Sanitise task names (trim whitespace/tabs) to prevent silent mismatches (Bug B4)
            foreach (var t in _taskSequence.tasks)
                t.taskName = t.taskName.Trim();

            // ── LOG 1: Task sequence initialized ─────────────────────────────
            Debug.Log($"[TaskProgressionManager] Task sequence initialized with {TotalTaskCount} tasks:");
            for (int i = 0; i < TotalTaskCount; i++)
                Debug.Log($"  [{i}] '{_taskSequence.tasks[i].taskName}'  phase={_taskSequence.tasks[i].phase}");
        }

        private void Start()
        {
            if (_taskSequence == null) return;

            // ── LOG 2: First task loaded ──────────────────────────────────────
            Debug.Log($"[TaskProgressionManager] Starting. First task: '{CurrentTask?.taskName}'. " +
                      $"Total tasks: {TotalTaskCount}. Broadcasting OnNextTaskStarted.");

            // Announce the first task to all subscribers (TaskUIManager should have subscribed by now)
            if (!AllTasksDone)
                OnNextTaskStarted?.Invoke(CurrentTask);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Called by EquipmentInteraction when the player successfully uses a piece of equipment.
        /// taskId must match the CURRENT task's taskName (case-sensitive after trim).
        /// Out-of-order calls are rejected with a log message.
        /// </summary>
        public void CompleteTask(string taskId)
        {
            // ── LOG 8: Task manager received completion event ─────────────────
            Debug.Log($"[TaskProgressionManager] CompleteTask called with id='{taskId}'. " +
                      $"Current task='{CurrentTask?.taskName}'. AllDone={AllTasksDone}");

            if (_taskSequence == null)
            {
                Debug.LogError("[TaskProgressionManager] Cannot complete task — TaskSequence is null!", this);
                return;
            }

            if (AllTasksDone)
            {
                Debug.Log($"[TaskProgressionManager] CompleteTask('{taskId}') ignored — all tasks already done.");
                return;
            }

            // Trim incoming id too, for safety
            string trimmedId = taskId?.Trim() ?? string.Empty;

            // Guard: only accept the CURRENT task's id
            if (CurrentTask.taskName != trimmedId)
            {
                Debug.LogWarning($"[TaskProgressionManager] MISMATCH — received '{trimmedId}' " +
                                 $"but current task is '{CurrentTask.taskName}'. " +
                                 "Ensure EquipmentInteraction.taskId matches TaskData.taskName exactly.");
                return;
            }

            // Guard: prevent duplicate completion
            if (_completedTaskIds.Contains(trimmedId))
            {
                Debug.Log($"[TaskProgressionManager] Task '{trimmedId}' was already completed.");
                return;
            }

            _completedTaskIds.Add(trimmedId);
            int completedIndex = _currentTaskIndex;
            _currentTaskIndex++;

            // ── LOG 9: Task index incremented ────────────────────────────────
            Debug.Log($"[TaskProgressionManager] Task '{trimmedId}' completed (index {completedIndex}). " +
                      $"CompletedCount={CompletedCount}/{TotalTaskCount}. NewIndex={_currentTaskIndex}");

            OnTaskCompleted?.Invoke(completedIndex);

            if (AllTasksDone)
            {
                Debug.Log("[TaskProgressionManager] *** ALL TASKS COMPLETED! ***");
                OnAllTasksCompleted?.Invoke();
            }
            else
            {
                Debug.Log($"[TaskProgressionManager] Next task: '{CurrentTask.taskName}'");
                OnNextTaskStarted?.Invoke(CurrentTask);
            }
        }

        /// <summary>Returns true if the given taskName (after trim) is the one currently active.</summary>
        public bool IsCurrentTask(string taskId)
        {
            if (AllTasksDone || CurrentTask == null) return false;
            return CurrentTask.taskName == (taskId?.Trim() ?? string.Empty);
        }

        /// <summary>
        /// Returns the taskName at the given index, or an empty string if out of range.
        /// Used by TaskListUI to display the full list without direct SO access.
        /// </summary>
        public string GetTaskName(int index)
        {
            if (_taskSequence == null) return string.Empty;
            if (index < 0 || index >= _taskSequence.tasks.Length) return string.Empty;
            return _taskSequence.tasks[index].taskName;
        }
    }
}
