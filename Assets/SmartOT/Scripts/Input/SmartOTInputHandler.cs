using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SmartOT.Input
{
    /// <summary>
    /// Single point of entry for the SmartOT "Use" action.
    ///
    /// How it works:
    ///   - Reads the existing InputSystem_Actions asset via an InputActionReference
    ///     (drag the Player/Use action into the Inspector field).
    ///   - Raises OnUseStarted  when the button first goes down.
    ///   - Raises OnUsePerformed when the Hold interaction completes (the full hold).
    ///   - Raises OnUseCancelled when the button is released before the hold threshold.
    ///
    /// Other scripts subscribe to these events; they never touch the Input System directly.
    /// AutoHand grabs/releases are handled entirely by the AutoHand package — not here.
    /// </summary>
    public class SmartOTInputHandler : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("Drag the Player/Use action from InputSystem_Actions here.")]
        [SerializeField] private InputActionReference _useActionReference;

        // ── Public events ─────────────────────────────────────────────────────
        /// <summary>Fired the moment the Use button is pressed (before hold threshold).</summary>
        public event Action OnUseStarted;

        /// <summary>Fired when the Hold interaction completes (button held long enough).</summary>
        public event Action OnUsePerformed;

        /// <summary>Fired when the button is released before hold completes.</summary>
        public event Action OnUseCancelled;

        // ── Private ───────────────────────────────────────────────────────────
        private InputAction _useAction;

        private void Awake()
        {
            if (_useActionReference == null)
            {
                Debug.LogError("[SmartOTInputHandler] UseActionReference is not assigned. " +
                               "Drag the Player/Use action from InputSystem_Actions into the Inspector.", this);
                enabled = false;
                return;
            }

            _useAction = _useActionReference.action;
        }

        private void OnEnable()
        {
            if (_useAction == null) return;

            _useAction.started   += HandleStarted;
            _useAction.performed += HandlePerformed;
            _useAction.canceled  += HandleCancelled;
            _useAction.Enable();
        }

        private void OnDisable()
        {
            if (_useAction == null) return;

            _useAction.started   -= HandleStarted;
            _useAction.performed -= HandlePerformed;
            _useAction.canceled  -= HandleCancelled;
            // Do NOT disable the action here — other systems may share it.
        }

        // ── Callbacks ─────────────────────────────────────────────────────────
        private void HandleStarted(InputAction.CallbackContext ctx)
        {
            // ── LOG 3 (input level): Use action received ───────────────────────
            Debug.Log("[SmartOTInputHandler] Use action — STARTED (button pressed).");
            OnUseStarted?.Invoke();
        }

        private void HandlePerformed(InputAction.CallbackContext ctx)
        {
            OnUsePerformed?.Invoke();
        }

        private void HandleCancelled(InputAction.CallbackContext ctx)
        {
            OnUseCancelled?.Invoke();
        }
    }
}
