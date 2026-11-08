using UnityEngine;
using UnityEngine.UI;

namespace SmartOT.UI
{
    /// <summary>
    /// Converts the existing Screen Space – Overlay Canvas to World Space at runtime
    /// and keeps it positioned comfortably in front of the XR/VR camera.
    ///
    /// Place this component on the existing "Main UI" Canvas GameObject.
    ///
    /// Inspector setup:
    ///   _cameraTag       → leave as "MainCamera" (Quest assigns this automatically).
    ///   _distance        → distance in metres in front of the camera  (default 1.5).
    ///   _verticalOffset  → metres below the camera centre             (default -0.25).
    ///   _canvasWidth     → world-space width of the canvas in metres  (default 0.6).
    ///   _smoothSpeed     → lerp speed for following (0 = instant, higher = faster, default 8).
    ///
    /// How it works:
    ///   • In Awake it switches the Canvas to World Space and sets a physical size that
    ///     reads comfortably at _distance.
    ///   • Every LateUpdate it smoothly moves/rotates the canvas to stay in front of
    ///     the camera, slightly below the centre of view.
    ///   • The canvas is sized and scaled so the existing TMP text renders at a legible
    ///     font size on Quest 2 (font 36pt at 1/300 scale = ~12 cm letters at 1.5 m).
    ///
    /// Quest 2 notes:
    ///   • Quest automatically tags the centre-eye camera "MainCamera".
    ///   • World Space Canvas renders in both eyes as a 3D object — no stereo issues.
    ///   • GraphicRaycaster is not needed for display-only UI; it is disabled at runtime.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [AddComponentMenu("SmartOT/VR Head Follower")]
    public class VRHeadFollower : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Camera")]
        [Tooltip("Tag of the XR camera. Quest 2 uses 'MainCamera'. Change only if your camera has a different tag.")]
        [SerializeField] private string _cameraTag = "MainCamera";

        [Header("Position")]
        [Tooltip("Metres in front of the camera where the HUD sits.")]
        [SerializeField] private float _distance = 1.5f;

        [Tooltip("Metres below the camera's forward centre. Positive = lower.")]
        [SerializeField] private float _verticalOffset = -0.25f;

        [Header("Size")]
        [Tooltip("World-space width of the canvas in metres.")]
        [SerializeField] private float _canvasWidth = 0.6f;

        [Tooltip("World-space height of the canvas in metres.")]
        [SerializeField] private float _canvasHeight = 0.25f;

        [Header("Smoothing")]
        [Tooltip("LateUpdate lerp speed for position/rotation. 0 = instant snap, 10 = very responsive.")]
        [SerializeField] private float _smoothSpeed = 8f;

        // ── State ─────────────────────────────────────────────────────────────
        private Canvas    _canvas;
        private Camera    _vrCamera;
        private RectTransform _rectTransform;

        // Reference pixel size used by the canvas (must match what TMP fonts were authored at)
        // The existing TMP slots were sized at 200×50 reference pixels.
        // We set the RectTransform in those same reference units and use localScale to map
        // those units to world metres.
        private const float ReferenceWidth  = 800f;
        private const float ReferenceHeight = 300f;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            _canvas       = GetComponent<Canvas>();
            _rectTransform = GetComponent<RectTransform>();

            ConfigureCanvas();
            FindCamera();
        }

        private void Start()
        {
            // Snap to the correct position immediately on first frame.
            if (_vrCamera != null)
                SnapToCamera();
        }

        private void LateUpdate()
        {
            if (_vrCamera == null)
            {
                FindCamera();
                if (_vrCamera == null) return;
            }

            SmoothFollowCamera();
        }

        // ── Setup ─────────────────────────────────────────────────────────────

        private void ConfigureCanvas()
        {
            // Switch to World Space so the Quest headset can see it.
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = null; // not needed for World Space

            // Disable GraphicRaycaster — HUD is display-only, no input needed.
            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;

            // Disable CanvasScaler — we control size via RectTransform + localScale.
            var scaler = GetComponent<CanvasScaler>();
            if (scaler != null) scaler.enabled = false;

            // Set the RectTransform size in reference pixels.
            _rectTransform.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);

            // Scale: map reference pixels → world metres.
            float scaleX = _canvasWidth  / ReferenceWidth;
            float scaleY = _canvasHeight / ReferenceHeight;
            float scale  = Mathf.Min(scaleX, scaleY); // uniform scale so text isn't stretched
            _rectTransform.localScale = new Vector3(scale, scale, scale);

            Debug.Log($"[VRHeadFollower] Canvas configured — WorldSpace, size=({ReferenceWidth}×{ReferenceHeight}) " +
                      $"ref-px, scale={scale:F5} → {_canvasWidth:F2}m × {_canvasHeight:F2}m in world.");
        }

        private void FindCamera()
        {
            var camObj = GameObject.FindWithTag(_cameraTag);
            if (camObj != null)
                _vrCamera = camObj.GetComponent<Camera>();

            if (_vrCamera == null)
                Debug.LogWarning($"[VRHeadFollower] No Camera found with tag '{_cameraTag}'. " +
                                 "The HUD will not follow. Check the camera tag in the Inspector.");
            else
                Debug.Log($"[VRHeadFollower] Found VR camera: '{_vrCamera.name}'.");
        }

        // ── Follow Logic ──────────────────────────────────────────────────────

        private void SnapToCamera()
        {
            transform.position = TargetPosition();
            transform.rotation = TargetRotation();
        }

        private void SmoothFollowCamera()
        {
            float t = Mathf.Clamp01(_smoothSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, TargetPosition(), t);
            transform.rotation = Quaternion.Slerp(transform.rotation, TargetRotation(), t);
        }

        private Vector3 TargetPosition()
        {
            Transform cam = _vrCamera.transform;
            // Start from a point directly in front of the camera at _distance.
            Vector3 forward = cam.forward;
            Vector3 pos     = cam.position + forward * _distance;
            // Apply vertical offset in world-up direction (not camera-up) for comfort.
            pos += Vector3.up * _verticalOffset;
            return pos;
        }

        private Quaternion TargetRotation()
        {
            // Face the camera (billboard: rotate so canvas faces camera).
            Vector3 toCamera = _vrCamera.transform.position - transform.position;
            if (toCamera == Vector3.zero) return transform.rotation;
            // Keep the canvas upright (no roll) by locking world-up.
            return Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
        }
    }
}
