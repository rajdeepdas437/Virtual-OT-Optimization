using UnityEngine;
using SmartOT.Core;

namespace SmartOT.Core
{
    /// <summary>
    /// ScriptableObject that holds the ordered list of tasks for the entire procedure.
    ///
    /// Create one via:  Assets ▶ Create ▶ SmartOT ▶ Task Sequence
    ///
    /// Add entries in the Inspector.  TaskProgressionManager reads from this asset at runtime.
    /// To add more tasks later, simply add more TaskData entries here — no code changes needed.
    /// </summary>
    [CreateAssetMenu(fileName = "TaskSequence", menuName = "SmartOT/Task Sequence")]
    public class TaskSequence : ScriptableObject
    {
        [Tooltip("All tasks in the order they must be completed. Add more rows here to extend the sequence.")]
        public TaskData[] tasks;
    }
}
