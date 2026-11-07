using System;
using UnityEngine;

namespace SmartOT.Core
{
    /// <summary>
    /// Data class describing a single task in the SmartOT procedure.
    /// Stored in a TaskSequence ScriptableObject and read by TaskProgressionManager.
    /// </summary>
    [Serializable]
    public class TaskData
    {
        [Tooltip("Short display name shown in the HUD.")]
        public string taskName;

        [Tooltip("Longer instruction text shown in the HUD.")]
        [TextArea(2, 4)]
        public string taskDescription;

        [Tooltip("Which phase (1-4) this task belongs to.")]
        public int phase = 1;
    }
}
