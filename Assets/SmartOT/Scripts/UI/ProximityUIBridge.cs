using UnityEngine;
using SmartOT.Interactions;
using SmartOT.UI;

namespace SmartOT.UI
{
    /// <summary>
    /// Thin bridge between a ProximityInteractor and the TaskUIManager.
    ///
    /// Place this on every interactive GameObject alongside a ProximityInteractor.
    /// It wires the proximity events to the UI without coupling the two classes directly.
    ///
    /// Single responsibility: forward OnPlayerEntered / OnPlayerExited to TaskUIManager.
    ///
    /// Inspector setup:
    ///   - Assign the TaskUIManager from the scene.
    ///   - Assign the ProximityInteractor (auto-found on same GameObject if empty).
    /// </summary>
    public class ProximityUIBridge : MonoBehaviour
    {
        [SerializeField] private ProximityInteractor _proximityInteractor;
        [SerializeField] private TaskUIManager _taskUIManager;

        private void Awake()
        {
            if (_proximityInteractor == null)
                _proximityInteractor = GetComponent<ProximityInteractor>();

            if (_proximityInteractor == null)
                Debug.LogError("[ProximityUIBridge] ProximityInteractor not found on this GameObject.", this);

            if (_taskUIManager == null)
                _taskUIManager = FindFirstObjectByType<TaskUIManager>();

            if (_taskUIManager == null)
                Debug.LogError("[ProximityUIBridge] TaskUIManager not found in scene.", this);
        }

        private void OnEnable()
        {
            if (_proximityInteractor != null)
            {
                _proximityInteractor.OnPlayerEntered += _taskUIManager.ShowPrompt;
                _proximityInteractor.OnPlayerExited  += _taskUIManager.HidePrompt;
            }
        }

        private void OnDisable()
        {
            if (_proximityInteractor != null)
            {
                _proximityInteractor.OnPlayerEntered -= _taskUIManager.ShowPrompt;
                _proximityInteractor.OnPlayerExited  -= _taskUIManager.HidePrompt;
            }
        }
    }
}
