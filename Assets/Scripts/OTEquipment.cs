using UnityEngine;

public enum EquipmentCategory
{
    LifeSupportAnesthesia,
    SurgicalInstrument,
    MonitoringEmergency,
    PeripherySupport
}

public class OTEquipment : MonoBehaviour
{
    [Header("Equipment Details")]
    public string equipmentName;
    public EquipmentCategory category;

    [Range(1, 10)]
    public int accessFrequency = 5; // How often staff need to reach this during a procedure (1-10)
    
    [Range(1, 10)]
    public int criticalityWeight = 5; // How critical fast access is in an emergency (1-10)

    // Combined priority weight for optimization cost function
    public float GetPriorityWeight()
    {
        return accessFrequency * criticalityWeight;
    }

    private void OnDrawGizmos()
    {
        // Visual indicators in the Scene view based on urgency/criticality
        Gizmos.color = criticalityWeight > 7 ? Color.red : (criticalityWeight > 4 ? Color.yellow : Color.green);
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
    }
}