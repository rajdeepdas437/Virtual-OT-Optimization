using UnityEngine;
using SmartOT.Equipment;

namespace SmartOT.Equipment
{
    /// <summary>
    /// Swaps the patient from the normal abdomen model to the inflated abdomen model
    /// when the CO2 insufflator is successfully activated.
    ///
    /// Place this component on the SAME GameObject as the CO2 insufflator's
    /// EquipmentInteraction component. It hooks EquipmentInteraction.OnEquipmentActivated
    /// in Start and swaps the two patient model GameObjects on that event.
    ///
    /// Inspector setup:
    ///   - _patientNormal   → drag the Patient_Normal   GameObject.
    ///   - _patientInflated → drag the Patient_Inflated GameObject.
    ///   Both are in the scene already (Patient_Normal active, Patient_Inflated inactive).
    /// </summary>
    [AddComponentMenu("SmartOT/Patient Model Swapper")]
    public class PatientModelSwapper : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Patient Models")]
        [Tooltip("The normal (pre-insufflation) patient model — active at scene start.")]
        [SerializeField] private GameObject _patientNormal;

        [Tooltip("The inflated (post-insufflation) patient model — inactive at scene start.")]
        [SerializeField] private GameObject _patientInflated;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            if (_patientNormal == null)
                Debug.LogError("[PatientModelSwapper] _patientNormal not assigned.", this);
            if (_patientInflated == null)
                Debug.LogError("[PatientModelSwapper] _patientInflated not assigned.", this);
        }

        private void Start()
        {
            var equipment = GetComponent<EquipmentInteraction>();
            if (equipment == null)
            {
                Debug.LogError("[PatientModelSwapper] No EquipmentInteraction on this GameObject. " +
                               "Add EquipmentInteraction (taskId = 'Perform CO2 Insufflation') " +
                               "to the same GO as this component.", this);
                return;
            }

            equipment.OnEquipmentActivated += SwapPatientModel;
        }

        private void OnDestroy()
        {
            var equipment = GetComponent<EquipmentInteraction>();
            if (equipment != null)
                equipment.OnEquipmentActivated -= SwapPatientModel;
        }

        // ── Swap ──────────────────────────────────────────────────────────────
        private void SwapPatientModel()
        {
            if (_patientNormal != null)
            {
                _patientNormal.SetActive(false);
                Debug.Log("[PatientModelSwapper] Patient_Normal hidden.");
            }

            if (_patientInflated != null)
            {
                _patientInflated.SetActive(true);
                Debug.Log("[PatientModelSwapper] Patient_Inflated shown.");
            }
        }
    }
}
