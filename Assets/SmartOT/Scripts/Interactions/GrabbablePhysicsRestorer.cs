using System.Collections;
using UnityEngine;
using Autohand;

namespace SmartOT.Interactions
{
    /// <summary>
    /// Manages Rigidbody physics state and out-of-bounds safety recovery for grabbable equipment.
    ///
    /// Key Capabilities:
    /// 1. Table / Surface Physics: Normal dynamic physics with gravity enabled while resting.
    /// 2. AutoHand Integration: Hands have full control while held; physics restorer does not interfere.
    /// 3. Attachment Safety: When attached to a PlacePoint (AttachmentZone on patient/table),
    ///    locks the Rigidbody (isKinematic = true, useGravity = false, velocity = 0) so it never
    ///    falls off or jitters.
    /// 4. Detachment Handling: If detached, restores normal dynamic gravity physics.
    /// 5. Out-of-Bounds Recovery: Automatically respawns dropped or launched objects back to their
    ///    spawn point when ungrabbed and unattached, resetting velocities to zero.
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    [AddComponentMenu("SmartOT/Grabbable Physics Restorer")]
    public class GrabbablePhysicsRestorer : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────
        [Header("Out-of-Bounds Recovery")]
        [Tooltip("Enable out-of-bounds recovery for dropped/launched equipment.")]
        [SerializeField] private bool _enableRecovery = true;

        [Tooltip("Optional custom recovery transform. If null, uses initial spawn position/rotation.")]
        [SerializeField] private Transform _recoveryPoint;

        [Tooltip("Y coordinate below which the object is considered out-of-bounds (e.g. fallen through the floor).")]
        [SerializeField] private float _outOfBoundsY = -0.5f;

        [Tooltip("Maximum allowed distance from spawn position before considered accidentally launched.")]
        [SerializeField] private float _maxDistance = 10f;

        [Tooltip("Recover if the object is dropped and rests on the floor.")]
        [SerializeField] private bool _recoverOnFloor = true;

        [Tooltip("Y threshold below which the object is considered on the floor (table height is ~1.2m, floor is 0m).")]
        [SerializeField] private float _floorYThreshold = 0.35f;

        [Tooltip("Seconds the object must rest on the floor before recovering.")]
        [SerializeField] private float _floorRecoverDelay = 2.0f;

        [Header("Debug")]
        [Tooltip("Enable detailed console logging for this component.")]
        [SerializeField] private bool _debugLogging = false;

        // ── State ─────────────────────────────────────────────────────────────────
        private Grabbable _grabbable;
        private Rigidbody _body;

        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;

        private PlacePoint _currentPlacePoint;
        private bool _isAttached = false;
        private float _floorTimer = 0f;

        // ── Unity Lifecycle ───────────────────────────────────────────────────────
        private void Awake()
        {
            _grabbable = GetComponent<Grabbable>();
            if (_grabbable == null)
            {
                Debug.LogError($"[GrabbablePhysicsRestorer] No Grabbable found on '{name}'. Component disabled.", this);
                enabled = false;
                return;
            }

            _body = _grabbable.body != null ? _grabbable.body : GetComponent<Rigidbody>();
            if (_body == null)
            {
                Debug.LogError($"[GrabbablePhysicsRestorer] No Rigidbody found on '{name}'. Component disabled.", this);
                enabled = false;
                return;
            }

            // Record initial spawn transform
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;

            // Ensure normal physics at startup if not attached to an AttachmentZone
            if (!IsAttached())
            {
                _body.isKinematic = false;
                _body.useGravity = true;
            }

            if (_debugLogging)
                Debug.Log($"[GrabbablePhysicsRestorer] '{name}' initialized. SpawnPos={_spawnPosition}. Initial physics: isKinematic={_body.isKinematic}, useGravity={_body.useGravity}");
        }

        private void OnEnable()
        {
            if (_grabbable != null)
            {
                _grabbable.OnGrabEvent += OnGrabbed;
                _grabbable.OnReleaseEvent += OnReleased;
                _grabbable.OnPlacePointAddEvent += OnPlacePointAdded;
                _grabbable.OnPlacePointRemoveEvent += OnPlacePointRemoved;
            }
        }

        private void OnDisable()
        {
            if (_grabbable != null)
            {
                _grabbable.OnGrabEvent -= OnGrabbed;
                _grabbable.OnReleaseEvent -= OnReleased;
                _grabbable.OnPlacePointAddEvent -= OnPlacePointAdded;
                _grabbable.OnPlacePointRemoveEvent -= OnPlacePointRemoved;
            }
        }

        // ── AutoHand Hand Events ──────────────────────────────────────────────────
        private void OnGrabbed(Hand hand, Grabbable grab)
        {
            _floorTimer = 0f;
            // AutoHand manages Rigidbody behaviour while grabbed (Requirement 3).
            if (_debugLogging)
                Debug.Log($"[GrabbablePhysicsRestorer] '{name}' grabbed by '{hand.name}'. AutoHand managing physics.");
        }

        private void OnReleased(Hand hand, Grabbable grab)
        {
            _floorTimer = 0f;
            StartCoroutine(RestorePhysicsNextFrame(grab));
        }

        private IEnumerator RestorePhysicsNextFrame(Grabbable grab)
        {
            yield return new WaitForFixedUpdate();

            // Safety: if the object was immediately re-grabbed, do not interfere.
            if (grab != null && grab.IsHeld())
                yield break;

            // Requirement 4: If attached to an AttachmentZone, keep it kinematic and disable gravity.
            if (IsAttached())
            {
                SetAttachedPhysics(true);
                if (_debugLogging)
                    Debug.Log($"[GrabbablePhysicsRestorer] '{name}' placed in AttachmentZone '{_currentPlacePoint?.name}'. Keeping kinematic, gravity disabled.");
                yield break;
            }

            // Requirement 2: Keep normal gravity and physics enabled for equipment resting on tables/surfaces.
            SetAttachedPhysics(false);
            if (_debugLogging)
                Debug.Log($"[GrabbablePhysicsRestorer] '{name}' released. Restored dynamic gravity physics.");
        }

        // ── PlacePoint (AttachmentZone) Events ─────────────────────────────────────
        private void OnPlacePointAdded(PlacePoint point, Grabbable grab)
        {
            _currentPlacePoint = point;
            _isAttached = true;
            _floorTimer = 0f;

            // Requirement 4: When equipment is successfully attached through an AttachmentZone,
            // make it kinematic and disable gravity so it stays fixed.
            SetAttachedPhysics(true);

            if (_debugLogging)
                Debug.Log($"[GrabbablePhysicsRestorer] '{name}' successfully attached to AttachmentZone '{point.name}'. Kinematic locked.");
        }

        private void OnPlacePointRemoved(PlacePoint point, Grabbable grab)
        {
            _currentPlacePoint = null;
            _isAttached = false;

            // Requirement 5: If an attached object is detached, restore normal gravity and physics.
            if (_grabbable != null && !_grabbable.IsHeld())
            {
                SetAttachedPhysics(false);
                if (_debugLogging)
                    Debug.Log($"[GrabbablePhysicsRestorer] '{name}' detached from AttachmentZone '{point.name}'. Restored dynamic gravity physics.");
            }
        }

        // ── Physics State Helper ───────────────────────────────────────────────────
        private void SetAttachedPhysics(bool attached)
        {
            if (_body == null)
            {
                _body = _grabbable != null ? _grabbable.body : GetComponent<Rigidbody>();
                if (_body == null) return;
            }

            if (attached)
            {
                #if UNITY_6000_0_OR_NEWER
                _body.linearVelocity = Vector3.zero;
                #else
                _body.velocity = Vector3.zero;
                #endif
                _body.angularVelocity = Vector3.zero;
                _body.isKinematic = true;
                _body.useGravity = false;
            }
            else
            {
                _body.isKinematic = false;
                _body.useGravity = true;
                _body.WakeUp();
            }
        }

        /// <summary>
        /// Checks if the equipment is currently attached to an AttachmentZone (PlacePoint).
        /// </summary>
        public bool IsAttached()
        {
            if (_isAttached) return true;
            if (_currentPlacePoint != null && _currentPlacePoint.placedObject == _grabbable) return true;
            if (_grabbable != null && _grabbable.placePoint != null && _grabbable.placePoint.placedObject == _grabbable) return true;
            return false;
        }

        // ── Out-of-Bounds & Recovery System ───────────────────────────────────────
        private void FixedUpdate()
        {
            if (!_enableRecovery) return;
            if (_grabbable == null || _body == null) return;

            // Requirement 6: Recover ONLY when they are not grabbed or attached.
            if (_grabbable.IsHeld() || IsAttached())
            {
                _floorTimer = 0f;
                return;
            }

            Vector3 currentPos = transform.position;
            Vector3 spawnPos = _recoveryPoint != null ? _recoveryPoint.position : _spawnPosition;

            // Condition 1: Dropped out of bounds (fallen below floor / into void)
            if (currentPos.y < _outOfBoundsY)
            {
                if (_debugLogging)
                    Debug.Log($"[GrabbablePhysicsRestorer] '{name}' out-of-bounds (y={currentPos.y:F2} < {_outOfBoundsY:F2}). Recovering.");
                Recover();
                return;
            }

            // Condition 2: Accidentally launched out of bounds (far from spawn)
            if (Vector3.Distance(currentPos, spawnPos) > _maxDistance)
            {
                if (_debugLogging)
                    Debug.Log($"[GrabbablePhysicsRestorer] '{name}' launched beyond max distance ({Vector3.Distance(currentPos, spawnPos):F2}m > {_maxDistance:F2}m). Recovering.");
                Recover();
                return;
            }

            // Condition 3: Dropped on the floor (y < floor threshold)
            if (_recoverOnFloor && currentPos.y < _floorYThreshold)
            {
                _floorTimer += Time.fixedDeltaTime;
                if (_floorTimer >= _floorRecoverDelay)
                {
                    if (_debugLogging)
                        Debug.Log($"[GrabbablePhysicsRestorer] '{name}' rested on floor for {_floorTimer:F1}s. Recovering.");
                    Recover();
                }
            }
            else
            {
                _floorTimer = 0f;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_enableRecovery) return;
            if (_grabbable != null && _grabbable.IsHeld()) return;
            if (IsAttached()) return;

            if (other.CompareTag("OutOfBounds") || other.GetComponent<EquipmentRecoveryZone>() != null)
            {
                if (_debugLogging)
                    Debug.Log($"[GrabbablePhysicsRestorer] '{name}' entered recovery trigger '{other.name}'. Recovering.");
                Recover();
            }
        }

        /// <summary>
        /// Respawns the equipment back to its spawn position/rotation and resets linear & angular velocity.
        /// Can be tested via context menu in the Inspector.
        /// </summary>
        [ContextMenu("Test Recover")]
        public void Recover()
        {
            // Guard: Recover only when not grabbed or attached
            if (_grabbable != null && _grabbable.IsHeld())
            {
                if (_debugLogging)
                    Debug.Log($"[GrabbablePhysicsRestorer] '{name}' Recover aborted — object is currently held.");
                return;
            }

            if (IsAttached())
            {
                if (_debugLogging)
                    Debug.Log($"[GrabbablePhysicsRestorer] '{name}' Recover aborted — object is currently attached.");
                return;
            }

            Vector3 targetPos = _recoveryPoint != null ? _recoveryPoint.position : _spawnPosition;
            Quaternion targetRot = _recoveryPoint != null ? _recoveryPoint.rotation : _spawnRotation;

            if (_body != null)
            {
                // Requirement 7: Reset velocity and angular velocity after recovery.
                #if UNITY_6000_0_OR_NEWER
                _body.linearVelocity = Vector3.zero;
                #else
                _body.velocity = Vector3.zero;
                #endif
                _body.angularVelocity = Vector3.zero;
                _body.isKinematic = false;
                _body.useGravity = true;
                _body.position = targetPos;
                _body.rotation = targetRot;
            }

            transform.position = targetPos;
            transform.rotation = targetRot;

            if (_body != null)
            {
                #if UNITY_6000_0_OR_NEWER
                _body.linearVelocity = Vector3.zero;
                #else
                _body.velocity = Vector3.zero;
                #endif
                _body.angularVelocity = Vector3.zero;
                _body.WakeUp();
            }

            _floorTimer = 0f;

            if (_debugLogging)
                Debug.Log($"[GrabbablePhysicsRestorer] '{name}' successfully recovered to {targetPos}. Velocities reset to zero.");
        }
    }
}
