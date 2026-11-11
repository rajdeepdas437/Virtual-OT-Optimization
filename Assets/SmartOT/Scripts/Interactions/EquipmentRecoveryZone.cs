using UnityEngine;

namespace SmartOT.Interactions
{
    /// <summary>
    /// Optional trigger volume for out-of-bounds equipment recovery.
    /// Place this component on any trigger Collider GameObject (e.g., under the floor or around boundaries).
    /// When an unheld, unattached piece of equipment enters the trigger, it recovers automatically.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("SmartOT/Equipment Recovery Zone")]
    public class EquipmentRecoveryZone : MonoBehaviour
    {
        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var restorer = other.GetComponentInParent<GrabbablePhysicsRestorer>();
            if (restorer != null)
            {
                restorer.Recover();
            }
        }
    }
}
