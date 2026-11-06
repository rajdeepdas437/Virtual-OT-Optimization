using UnityEngine;
using System.Collections.Generic;

public class OTLayoutAnalyzer : MonoBehaviour
{
    [Header("Reference Points")]
    public Transform operatingTable; // Center of the OT workspace
    
    [Header("Discovered Equipment")]
    public List<OTEquipment> allEquipment = new List<OTEquipment>();

    [Header("Layout Metrics")]
    [SerializeField] private float totalWeightedCost;
    [SerializeField] private float maxAllowableDistance = 3.0f; // Target max distance for critical gear (meters)
    [SerializeField] private int bottleneckViolations = 0;

    private void Update()
    {
        if (operatingTable == null) return;
        
        EvaluateLayout();
    }

    [ContextMenu("Run Layout Evaluation")]
    public void EvaluateLayout()
    {
        // Refresh equipment list in scene
        allEquipment.Clear();
        allEquipment.AddRange(FindObjectsOfType<OTEquipment>());

        totalWeightedCost = 0f;
        bottleneckViolations = 0;

        foreach (var eq in allEquipment)
        {
            float distance = Vector3.Distance(operatingTable.position, eq.transform.position);
            
            // Calculate weighted transport/access cost
            float weight = eq.GetPriorityWeight();
            totalWeightedCost += distance * weight;

            // Check if high-priority life support equipment is placed too far away
            if (eq.criticalityWeight >= 7 && distance > maxAllowableDistance)
            {
                bottleneckViolations++;
                Debug.LogWarning($"[Layout Bottleneck] {eq.equipmentName} is too far ({distance:F2}m) for its high criticality!", eq.gameObject);
            }
        }

        Debug.Log($"[OT Layout Report] Total Weighted Access Cost: {totalWeightedCost:F2} | Critical Bottlenecks: {bottleneckViolations}");
    }
}
