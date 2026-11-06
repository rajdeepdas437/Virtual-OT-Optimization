using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(OTLayoutAnalyzer))]
public class OTSceneGenerator : MonoBehaviour
{
    [Header("Generator Controls")]
    [SerializeField] 
    private bool showGeneratorControls = true;
    
    [ContextMenu("Build OT Scene Now")]
    public void GenerateCompleteOT()
    {
        // Clean up any existing children to prevent duplication
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }

        // 1. Build Room Environment (6m x 6m x 3m standard OT)
        CreateRoom();

        // 2. Build Operating Table (Center)
        Transform opTable = CreateOperatingTable();

        // 3. Build Medical Equipment with proper Work System Design placement
        CreateEquipment("Anesthesia Machine", EquipmentCategory.LifeSupportAnesthesia, new Vector3(0f, 0f, 1.6f), new Vector3(0.8f, 1.4f, 0.6f), Color.cyan, 10, 9);
        CreateEquipment("Mayo Instrument Stand", EquipmentCategory.SurgicalInstrument, new Vector3(1.1f, 0f, 0.2f), new Vector3(0.6f, 0.9f, 0.5f), Color.white, 10, 8);
        CreateEquipment("Surgical Trolley (Scrub)", EquipmentCategory.SurgicalInstrument, new Vector3(-1.2f, 0f, -0.4f), new Vector3(0.7f, 0.9f, 0.9f), Color.lightGray, 9, 8);
        CreateEquipment("Patient Monitor", EquipmentCategory.MonitoringEmergency, new Vector3(0f, 1.8f, 1.4f), new Vector3(0.5f, 0.4f, 0.3f), Color.blue, 8, 9);
        CreateEquipment("Defibrillator / Crash Cart", EquipmentCategory.MonitoringEmergency, new Vector3(-2.2f, 0f, 2.2f), new Vector3(0.8f, 1.0f, 0.6f), Color.red, 3, 10);
        CreateEquipment("Suction Unit", EquipmentCategory.PeripherySupport, new Vector3(2.2f, 0f, -2.2f), new Vector3(0.5f, 0.8f, 0.5f), Color.yellow, 6, 6);
        CreateEquipment("Medical Gas Outlets Wall Unit", EquipmentCategory.PeripherySupport, new Vector3(0f, 1.5f, -2.9f), new Vector3(1.5f, 0.8f, 0.1f), Color.green, 5, 7);

        // 4. Configure Layout Analyzer Reference
        OTLayoutAnalyzer analyzer = GetComponent<OTLayoutAnalyzer>();
        if (analyzer != null)
        {
            analyzer.operatingTable = opTable;
            analyzer.EvaluateLayout();
        }

        Debug.Log("<color=green>[OT Generator]</color> Operation Theatre scene successfully generated and analyzed!");
    }

    private void CreateRoom()
    {
        GameObject room = new GameObject("OT_Room_Structure");
        room.transform.parent = transform;

        // Floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.parent = room.transform;
        floor.transform.position = new Vector3(0, -0.05f, 0);
        floor.transform.localScale = new Vector3(6f, 0.1f, 6f);
        SetMatColor(floor, new Color(0.85f, 0.85f, 0.9f));

        // Walls (Perimeter 6x6, height 3m)
        CreateWall(room.transform, "Wall_North", new Vector3(0, 1.5f, 3f), new Vector3(6f, 3f, 0.1f));
        CreateWall(room.transform, "Wall_South", new Vector3(0, 1.5f, -3f), new Vector3(6f, 3f, 0.1f));
        CreateWall(room.transform, "Wall_East", new Vector3(3f, 1.5f, 0), new Vector3(0.1f, 3f, 6f));
        CreateWall(room.transform, "Wall_West", new Vector3(-3f, 1.5f, 0), new Vector3(0.1f, 3f, 6f));
    }

    private void CreateWall(Transform parent, string name, Vector3 pos, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.parent = parent;
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        SetMatColor(wall, new Color(0.92f, 0.95f, 0.96f));
    }

    private Transform CreateOperatingTable()
    {
        GameObject tableGroup = new GameObject("OperatingTable_Center");
        tableGroup.transform.parent = transform;
        tableGroup.transform.position = Vector3.zero;

        // Base pedestal
        GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObj.transform.parent = tableGroup.transform;
        baseObj.transform.localPosition = new Vector3(0, 0.4f, 0);
        baseObj.transform.localScale = new Vector3(0.6f, 0.4f, 0.6f);
        SetMatColor(baseObj, Color.darkGray);

        // Mattress surface
        GameObject mattress = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mattress.name = "Table_Surface";
        mattress.transform.parent = tableGroup.transform;
        mattress.transform.localPosition = new Vector3(0, 0.85f, 0);
        mattress.transform.localScale = new Vector3(0.7f, 0.1f, 2.0f);
        SetMatColor(mattress, new Color(0.2f, 0.3f, 0.4f));

        // Overhead Surgical Light
        GameObject lightRig = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lightRig.name = "Surgical_Shadowless_Light";
        lightRig.transform.parent = tableGroup.transform;
        lightRig.transform.localPosition = new Vector3(0, 2.7f, 0);
        lightRig.transform.localScale = new Vector3(1.2f, 0.08f, 1.2f);
        SetMatColor(lightRig, Color.white);

        return tableGroup.transform;
    }

    private void CreateEquipment(string eqName, EquipmentCategory category, Vector3 pos, Vector3 scale, Color col, int freq, int critical)
    {
        GameObject eqObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        eqObj.name = eqName;
        eqObj.transform.parent = transform;
        eqObj.transform.position = pos;
        eqObj.transform.localScale = scale;
        SetMatColor(eqObj, col);

        // Attach functionality component
        OTEquipment eqComp = eqObj.AddComponent<OTEquipment>();
        eqComp.equipmentName = eqName;
        eqComp.category = category;
        eqComp.accessFrequency = freq;
        eqComp.criticalityWeight = critical;
    }

    private void SetMatColor(GameObject obj, Color col)
    {
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = col;
            rend.sharedMaterial = mat;
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(OTSceneGenerator))]
public class OTSceneGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        OTSceneGenerator generator = (OTSceneGenerator)target;
        GUILayout.Space(10);
        if (GUILayout.Button("🚀 Generate OT Layout Scene", GUILayout.Height(40)))
        {
            generator.GenerateCompleteOT();
        }
    }
}
#endif
