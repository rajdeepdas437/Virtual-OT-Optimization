using UnityEngine;
using SmartOT.Interactions;
using SmartOT.UI;

namespace SmartOT.UI
{
    /// <summary>
    /// Bridges PatientInspectionZone events to TaskUIManager.
    ///
    /// Mirrors the responsibility of ProximityUIBridge but targets
    /// PatientInspectionZone instead of ProximityInteractor.
    ///
    /// Place this on the Patient_InspectionZone GameObject alongside
    /// the PatientInspectionZone component.
    ///
    /// Inspector setup:
    ///   - _patientZone  → auto-found on same GameObject, or drag manually.
    ///   - _taskUIManager → auto-found in scene, or drag the Main UI GameObject.
    /// </summary>
    public class PatientZoneUIBridge : MonoBehaviour
    {
        [SerializeField] private PatientInspectionZone _patientZone;
        [SerializeField] private TaskUIManager         _taskUIManager;

        private void Awake()
        {
            if (_patientZone == null)
                _patientZone = GetComponent<PatientInspectionZone>();

            if (_patientZone == null)
                Debug.LogError("[PatientZoneUIBridge] PatientInspectionZone not found. " +
                               "Attach this script to the same GameObject as PatientInspectionZone.", this);

            if (_taskUIManager == null)
                _taskUIManager = FindFirstObjectByType<TaskUIManager>();

            if (_taskUIManager == null)
                Debug.LogError("[PatientZoneUIBridge] TaskUIManager not found in scene.", this);
        }

        private void OnEnable()
        {
            if (_patientZone != null && _taskUIManager != null)
            {
                _patientZone.OnPlayerEntered += _taskUIManager.ShowPrompt;
                _patientZone.OnPlayerExited  += _taskUIManager.HidePrompt;
            }
        }

        private void OnDisable()
        {
            if (_patientZone != null && _taskUIManager != null)
            {
                _patientZone.OnPlayerEntered -= _taskUIManager.ShowPrompt;
                _patientZone.OnPlayerExited  -= _taskUIManager.HidePrompt;
            }
        }
    }
}
