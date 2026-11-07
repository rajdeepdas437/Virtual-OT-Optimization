using UnityEngine;
using SmartOT.Core;
using SmartOT.Interactions;

namespace SmartOT.Equipment
{
    /// <summary>
    /// Surgical Lights activation controller.
    ///
    /// When the player activates the light switch (via EquipmentInteraction on the
    /// same or a nearby GO), this component enables the _surgicalLightsRoot GameObject,
    /// turning on all lights inside it.
    ///
    /// The _surgicalLightsRoot must be DISABLED in the Inspector at scene start.
    /// This script enables it via SetActive(true) on activation — no material swap,
    /// no shader change, no fake glow.
    ///
    /// Inspector setup (on the light switch / controller GameObject):
    ///   - _surgicalLightsRoot → drag the "Surgery Lights" (or equivalent) parent GO.
    ///   - Attach EquipmentInteraction on the SAME GameObject:
    ///       _taskId = "Switch On Surgical Lights"
    ///     SurgicalLightsController subscribes to EquipmentInteraction.OnEquipmentActivated.
    ///
    /// OR: If you prefer a standalone approach, also add ProximityInteractor to this GO
    /// and set SurgicalLightsController to work independently (see _standalone flag).
    /// </summary>
    [AddComponentMenu("SmartOT/Surgical Lights Controller")]
    public class SurgicalLightsController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Lights")]
        [Tooltip("The parent GameObject containing all surgical light objects. " +
                 "Must be DISABLED at scene start. This script enables it on activation.")]
        [SerializeField] private GameObject _surgicalLightsRoot;

        [Header("References")]
        [Tooltip("The EquipmentInteraction on this GameObject that handles task progression. " +
                 "Auto-found on same GO if not assigned.")]
        [SerializeField] private EquipmentInteraction _equipmentInteraction;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            if (_equipmentInteraction == null)
                _equipmentInteraction = GetComponent<EquipmentInteraction>();

            if (_equipmentInteraction == null)
                Debug.LogError("[SurgicalLightsController] No EquipmentInteraction found on this GameObject. " +
                               "Add one and set taskId = 'Switch On Surgical Lights'.", this);

            if (_surgicalLightsRoot == null)
                Debug.LogError("[SurgicalLightsController] _surgicalLightsRoot not assigned. " +
                               "Drag the Surgery Lights parent GameObject into the Inspector.", this);
            else if (_surgicalLightsRoot.activeSelf)
                Debug.LogWarning("[SurgicalLightsController] _surgicalLightsRoot is currently ACTIVE. " +
                                 "It should be disabled at scene start and enabled on activation.", this);
        }

        private void OnEnable()
        {
            if (_equipmentInteraction != null)
                _equipmentInteraction.OnEquipmentActivated += TurnOnLights;
        }

        private void OnDisable()
        {
            if (_equipmentInteraction != null)
                _equipmentInteraction.OnEquipmentActivated -= TurnOnLights;
        }

        // ── Activation ────────────────────────────────────────────────────────
        private void TurnOnLights()
        {
            if (_surgicalLightsRoot == null)
            {
                Debug.LogError("[SurgicalLightsController] Cannot turn on lights — _surgicalLightsRoot is null.", this);
                return;
            }

            _surgicalLightsRoot.SetActive(true);
            Debug.Log($"[SurgicalLightsController] Surgical lights enabled: '{_surgicalLightsRoot.name}'.");
        }
    }
}
