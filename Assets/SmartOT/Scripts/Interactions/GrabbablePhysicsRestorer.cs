using System.Collections;
using UnityEngine;
using Autohand;

namespace SmartOT.Interactions
{
    /// <summary>
    /// Restores Rigidbody physics state (isKinematic, useGravity) after AutoHand releases a Grabbable.
    ///
    /// PROBLEM THIS SOLVES:
    /// AutoHand's standard grab path does not set the grabbed object's isKinematic = true.
    /// However, in certain conditions (PlacePoint with makePlacedKinematic=true, or the
    /// Rigidbody starting kinematic in a previous session) the body can remain kinematic
    /// after release, causing the object to float in mid-air.
    ///
    /// HOW IT WORKS:
    /// 1. In Awake, records the Rigidbody's original isKinematic and useGravity values.
    ///    (These should be false/true for a normal physics object.)
    /// 2. Subscribes to Grabbable.OnReleaseEvent (the C# event, not the UnityEvent).
    /// 3. On release, waits one frame so AutoHand can apply throw velocity first,
    ///    then restores the recorded physics state.
    ///
    /// USAGE:
    /// Add this component alongside the Grabbable component on any object that suffers
    /// from floating after release. No Inspector wiring needed beyond adding the component.
    ///
    /// CONSTRAINTS:
    /// - Does NOT modify AutoHand package scripts.
    /// - Does NOT use an Update() loop.
    /// - Does NOT interfere with grabbing or throw velocity.
    /// - Skips restoration if the object is immediately grabbed again before the frame ends.
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    [AddComponentMenu("SmartOT/Grabbable Physics Restorer")]
    public class GrabbablePhysicsRestorer : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────
        [SerializeField, Tooltip("Enable detailed console logging for this component.")]
        private bool _debugLogging = true;

        // ── Private state ─────────────────────────────────────────────────────────
        private Grabbable _grabbable;
        private Rigidbody _body;

        /// <summary>Original isKinematic value read once at Awake (before any grab).</summary>
        private bool _originalIsKinematic;

        /// <summary>Original useGravity value read once at Awake (before any grab).</summary>
        private bool _originalUseGravity;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _grabbable = GetComponent<Grabbable>();
            if (_grabbable == null)
            {
                Debug.LogError($"[GrabbablePhysicsRestorer] No Grabbable found on '{name}'. Component disabled.", this);
                enabled = false;
                return;
            }

            // GrabbableBase.Awake() runs at DefaultExecutionOrder(-100), so body should
            // already be assigned by the time our Awake() runs.
            _body = _grabbable.body;
            if (_body == null)
            {
                Debug.LogError($"[GrabbablePhysicsRestorer] Grabbable on '{name}' has no Rigidbody assigned. " +
                               "Ensure a Rigidbody exists on this GameObject.", this);
                enabled = false;
                return;
            }

            // Record the intended physics state at startup.
            // For a normal pickable object this should be: isKinematic=false, useGravity=true.
            _originalIsKinematic = _body.isKinematic;
            _originalUseGravity  = _body.useGravity;

            if (_debugLogging)
                Debug.Log($"[GrabbablePhysicsRestorer] '{name}' — recorded initial state: " +
                          $"isKinematic={_originalIsKinematic}, useGravity={_originalUseGravity}");

            // Subscribe to the C# release event (fires inside Grabbable.OnRelease()).
            _grabbable.OnReleaseEvent += OnReleased;
        }

        private void OnDisable()
        {
            // Always unsubscribe to avoid stale delegates after the object is disabled.
            if (_grabbable != null)
                _grabbable.OnReleaseEvent -= OnReleased;
        }

        private void OnEnable()
        {
            // Re-subscribe if the object is re-enabled after being disabled.
            if (_grabbable != null)
                _grabbable.OnReleaseEvent += OnReleased;
        }

        private void OnDestroy()
        {
            if (_grabbable != null)
                _grabbable.OnReleaseEvent -= OnReleased;
        }

        // ── Release handler ───────────────────────────────────────────────────────

        /// <summary>
        /// Called by AutoHand immediately when the hand releases this Grabbable.
        /// We wait one frame before restoring physics so that AutoHand's SetThrowVelocity()
        /// (which runs just before OnReleaseEvent fires) can apply its force while the
        /// body may still be in whatever state AutoHand left it in.
        /// </summary>
        private void OnReleased(Hand hand, Grabbable grab)
        {
            if (_debugLogging)
                Debug.Log($"[GrabbablePhysicsRestorer] '{name}' released by hand '{hand.name}'. " +
                          $"Current isKinematic={_body.isKinematic}, useGravity={_body.useGravity}. " +
                          "Scheduling physics restore for next frame.");

            StartCoroutine(RestorePhysicsNextFrame(grab));
        }

        // ── Coroutine ─────────────────────────────────────────────────────────────

        private IEnumerator RestorePhysicsNextFrame(Grabbable grab)
        {
            // Wait one physics frame so AutoHand's throw velocity has been applied.
            yield return new WaitForFixedUpdate();

            // Safety: if the object was immediately grabbed again, do not interfere.
            if (grab != null && grab.IsHeld())
            {
                if (_debugLogging)
                    Debug.Log($"[GrabbablePhysicsRestorer] '{name}' was re-grabbed immediately — skipping restore.");
                yield break;
            }

            // Safety: Rigidbody may have been destroyed (e.g. DeactivateRigidbody).
            if (_body == null)
            {
                // Attempt to retrieve it again via GrabbableBase.body.
                _body = _grabbable != null ? _grabbable.body : null;
                if (_body == null)
                {
                    if (_debugLogging)
                        Debug.LogWarning($"[GrabbablePhysicsRestorer] '{name}' Rigidbody is null after release. " +
                                         "Cannot restore physics state.");
                    yield break;
                }
            }

            // Restore to the original pre-grab physics state.
            _body.isKinematic = _originalIsKinematic;
            _body.useGravity  = _originalUseGravity;

            // Wake the body so Unity's physics engine picks it up immediately.
            _body.WakeUp();

            if (_debugLogging)
                Debug.Log($"[GrabbablePhysicsRestorer] '{name}' — restored: " +
                          $"isKinematic={_body.isKinematic}, useGravity={_body.useGravity}. " +
                          "Object should now fall under gravity.");
        }
    }
}
