using UnityEngine;
using SmartOT.Interactions;
using SmartOT.UI;

namespace SmartOT.UI
{
    /// <summary>
    /// Bridges MonitorReadingsZone events to TaskUIManager.
    /// Place on the Monitor_ReadingsZone GameObject alongside MonitorReadingsZone.
    /// </summary>
    public class MonitorZoneUIBridge : MonoBehaviour
    {
        [SerializeField] private MonitorReadingsZone _monitorZone;
        [SerializeField] private TaskUIManager       _taskUIManager;

        private void Awake()
        {
            if (_monitorZone == null)
                _monitorZone = GetComponent<MonitorReadingsZone>();
            if (_monitorZone == null)
                Debug.LogError("[MonitorZoneUIBridge] MonitorReadingsZone not found on this GO.", this);

            if (_taskUIManager == null)
                _taskUIManager = FindFirstObjectByType<TaskUIManager>();
            if (_taskUIManager == null)
                Debug.LogError("[MonitorZoneUIBridge] TaskUIManager not found in scene.", this);
        }

        private void OnEnable()
        {
            if (_monitorZone != null && _taskUIManager != null)
            {
                _monitorZone.OnPlayerEntered += _taskUIManager.ShowPrompt;
                _monitorZone.OnPlayerExited  += _taskUIManager.HidePrompt;
            }
        }

        private void OnDisable()
        {
            if (_monitorZone != null && _taskUIManager != null)
            {
                _monitorZone.OnPlayerEntered -= _taskUIManager.ShowPrompt;
                _monitorZone.OnPlayerExited  -= _taskUIManager.HidePrompt;
            }
        }
    }
}
