// AutoHand + SmartOT implementation for the report pad inspection and placement.
// ------------------------------------------------------------
// Architecture:
//   * Uses AutoHand Grabbable events for grab/release detection.
//   * Uses PatientInspectionZone (always-active GO) for the patient proximity prompt.
//     (Patient_Normal itself is INACTIVE at scene start, so its trigger never fires.
//      PatientInspectionZone is a separate always-active sibling/peer GO.)
//   * Uses TaskProgressionManager to complete the "Inspect Patient" task.
//   * Uses PlacePoint (ReportPad_PlacementPoint) to snap the pad after inspection.
// ------------------------------------------------------------
using System;
using UnityEngine;
using Autohand;
using SmartOT.Core;
using SmartOT.Interactions;

namespace SmartOT.Equipment
{
    /// <summary>
    /// Handles the full report pad workflow:
    /// 1. Player grabs the pad (AutoHand).
    /// 2. Player enters the PatientInspectionZone trigger.
    /// 3. UI shows "Press Use to inspect patient".
    /// 4. Pressing Use while holding the pad completes the "Inspect Patient" task.
    /// 5. After inspection, the pad snaps ONLY when ALL conditions are met:
    ///      - inspection is completed
    ///      - pad has been moved ≥ moveAwayThreshold from its start position
    ///      - pad is released inside the ReportPad_PlacementPoint zone
    /// 6. The pad stays a fully normal AutoHand Grabbable throughout.
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    [RequireComponent(typeof(Rigidbody))]
    public class ReportPadInteraction : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("References")]
        [Tooltip("The PlacePoint that defines where the pad snaps after inspection. " +
                 "Drag the ReportPad_PlacementPoint GameObject here.")]
        [SerializeField] private PlacePoint placementPoint;

        [Tooltip("The PatientInspectionZone component (on a separate always-active GameObject " +
                 "positioned at the patient). Do NOT drag Patient_Normal here — it is inactive.")]
        [SerializeField] private PatientInspectionZone patientInspectionZone;

        [Tooltip("Minimum distance (m) the pad must be moved from its spawn position " +
                 "before snapping is allowed. Prevents accidental snap on first release.")]
        [SerializeField] private float moveAwayThreshold = 0.2f;

        // ── Private state ─────────────────────────────────────────────────────
        private Grabbable _grabbable;
        private Rigidbody _rigidbody;
        private Vector3   _initialPosition;
        private bool      _inspectionCompleted = false;
        private bool      _hasSnapped          = false;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            _grabbable = GetComponent<Grabbable>();
            _rigidbody = GetComponent<Rigidbody>();
            _initialPosition = transform.position;

            // Auto-find PlacePoint if not wired up.
            if (placementPoint == null)
            {
                placementPoint = FindFirstObjectByType<PlacePoint>();
                if (placementPoint == null)
                    Debug.LogError("[ReportPadInteraction] PlacementPoint not assigned and none found in scene. " +
                                   "Drag ReportPad_PlacementPoint into the Inspector.", this);
                else
                    Debug.LogWarning("[ReportPadInteraction] PlacementPoint auto-found — assign it directly for reliability.", this);
            }

            // Auto-find PatientInspectionZone if not wired up.
            if (patientInspectionZone == null)
            {
                patientInspectionZone = FindFirstObjectByType<PatientInspectionZone>();
                if (patientInspectionZone == null)
                    Debug.LogError("[ReportPadInteraction] PatientInspectionZone not found. " +
                                   "Create a Patient_InspectionZone GameObject with that component and assign it here.", this);
                else
                    Debug.LogWarning("[ReportPadInteraction] PatientInspectionZone auto-found — assign it directly for reliability.", this);
            }
        }

        private void OnEnable()
        {
            if (_grabbable != null)
            {
                _grabbable.OnGrabEvent    += OnGrabbed;
                _grabbable.OnReleaseEvent += OnReleased;
            }

            if (patientInspectionZone != null)
                patientInspectionZone.OnUseWhileInZone += OnPatientUsePressed;
        }

        private void OnDisable()
        {
            if (_grabbable != null)
            {
                _grabbable.OnGrabEvent    -= OnGrabbed;
                _grabbable.OnReleaseEvent -= OnReleased;
            }

            if (patientInspectionZone != null)
                patientInspectionZone.OnUseWhileInZone -= OnPatientUsePressed;
        }

        // ── AutoHand Events ───────────────────────────────────────────────────
        private void OnGrabbed(Hand hand, Grabbable grab)
        {
            // Allow snapping again when the pad is re-grabbed (e.g. picked up from placement).
            _hasSnapped = false;
            Debug.Log($"[ReportPadInteraction] Pad grabbed by '{hand.name}'. Snap flag reset.");
        }

        private void OnReleased(Hand hand, Grabbable grab)
        {
            if (!_inspectionCompleted) return;   // Inspection not done yet — no snap.
            if (_hasSnapped)           return;   // Already snapped once.

            float movedDist = Vector3.Distance(_initialPosition, transform.position);
            if (movedDist < moveAwayThreshold)
            {
                Debug.Log($"[ReportPadInteraction] Released but not moved enough " +
                          $"({movedDist:F2} m < threshold {moveAwayThreshold} m). No snap.");
                return;
            }

            if (placementPoint == null) return;

            float distToPoint = Vector3.Distance(transform.position, placementPoint.transform.position);
            bool  inRange     = distToPoint <= placementPoint.placeRadius;
            bool  canPlace    = placementPoint.CanPlace(_grabbable);

            Debug.Log($"[ReportPadInteraction] Release check — dist={distToPoint:F2}, " +
                      $"placeRadius={placementPoint.placeRadius:F2}, inRange={inRange}, canPlace={canPlace}");

            if (inRange && canPlace)
            {
                Debug.Log("[ReportPadInteraction] Conditions met — snapping pad to placement point.");
                placementPoint.Place(_grabbable);
                _hasSnapped = true;
            }
        }

        // ── Inspection Handler ────────────────────────────────────────────────
        private void OnPatientUsePressed()
        {
            // Guard: pad must be held by the player.
            if (_grabbable == null || !_grabbable.IsHeld())
            {
                Debug.Log("[ReportPadInteraction] Use pressed in patient zone but pad is not held — ignoring.");
                return;
            }

            if (_inspectionCompleted)
            {
                Debug.Log("[ReportPadInteraction] Inspection already done — ignoring repeat press.");
                return;
            }

            _inspectionCompleted = true;
            Debug.Log("[ReportPadInteraction] Patient inspected! Completing task 'Inspect Patient'.");

            // Hide the prompt immediately — don't wait for trigger exit.
            patientInspectionZone?.ForceHidePrompt();

            // Complete the task and advance the UI.
            TaskProgressionManager.Instance?.CompleteTask("Inspect Patient");
        }
    }
}
