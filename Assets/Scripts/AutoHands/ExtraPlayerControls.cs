using Autohand;
using UnityEngine;
using UnityEngine.InputSystem;

public class ExtraPlayerControls : MonoBehaviour
{
    private Rigidbody rb;
    [SerializeField] float jumpForce = 5f;

    InputAction openMap;
    [SerializeField] GameObject map;
    private bool isMapOpen;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        openMap = InputSystem.actions.FindAction("Map");
        isMapOpen=false;
    }

    
    void Update()
    {
        JumpTrigger();
        Run();
        OpenMap();
    }

    private static void Run()
    {
        if (Input.GetKey(KeyCode.LeftShift))
        {
            AutoHandPlayer.Instance.maxMoveSpeed = 5f;
        }
        else
        {
            AutoHandPlayer.Instance.maxMoveSpeed = 1.5f;
        }
    }

    private void JumpTrigger()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            AutoHandPlayer.Instance.Jump(jumpForce);
        }
    }

    private void OpenMap()
    {
        if(openMap.WasPressedThisFrame())
        {
            if(!isMapOpen)
            {
                map.SetActive(true);
                isMapOpen=true;
            }
            else
            {
                map.SetActive(false);
                isMapOpen=false;
            }
        }
    }
}
