using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TheForest.Building.Config;
using TheForest.Building.Core;
using TheForest.Building.Data;
using TheForest.Building.Events;

namespace TheForest.Building.Systems
{
    // ═══════════════════════════════════════════════════════════════════════════
    // DOOR PIECE — one spawned door panel (3 split logs)
    // ═══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// MonoBehaviour placed on a completed door GameObject.
    /// Handles open/close animation and lock state.
    /// Never calls other systems directly — raises DoorToggledEvent / DoorLockedEvent.
    /// </summary>
    public class DoorPiece : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float animDuration = 0.4f;

        [Header("Axis")]
        [SerializeField] private Transform pivotTransform; // hinge point

        private bool _isOpen;
        private bool _isLocked;
        private Coroutine _animRoutine;

        public bool IsOpen => _isOpen;
        public bool IsLocked => _isLocked;

        // ── Interaction ───────────────────────────────────────────────────────
        /// <summary>Player presses E on door — toggle open/close if not locked.</summary>
        public void Interact()
        {
            if (_isLocked) return;
            ToggleDoor();
        }

        public void ToggleDoor()
        {
            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _isOpen = !_isOpen;
            _animRoutine = StartCoroutine(AnimateDoor(_isOpen ? openAngle : 0f));
            EventBus<DoorToggledEvent>.Raise(new DoorToggledEvent(gameObject, _isOpen));
        }

        /// <summary>Called when a Stick is placed on the flat side — toggle lock.</summary>
        public void ToggleLock()
        {
            _isLocked = !_isLocked;
            EventBus<DoorLockedEvent>.Raise(new DoorLockedEvent(gameObject, _isLocked));
        }

        private IEnumerator AnimateDoor(float targetAngle)
        {
            if (pivotTransform == null) yield break;
            float startAngle = pivotTransform.localEulerAngles.y;
            float elapsed = 0f;
            while (elapsed < animDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
                pivotTransform.localEulerAngles = new Vector3(0f, Mathf.LerpAngle(startAngle, targetAngle, t), 0f);
                yield return null;
            }
            pivotTransform.localEulerAngles = new Vector3(0f, targetAngle, 0f);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PALISADE GATE — auto-created from 5th palisade log + 1 diagonal
    // ═══════════════════════════════════════════════════════════════════════════
    public class PalisadeGate : MonoBehaviour
    {
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float animDuration = 0.5f;
        [SerializeField] private Transform pivotTransform;

        private bool _isOpen;
        private Coroutine _animRoutine;

        public bool IsOpen => _isOpen;

        public void ToggleGate()
        {
            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _isOpen = !_isOpen;
            _animRoutine = StartCoroutine(Animate(_isOpen ? openAngle : 0f));
            EventBus<GateToggledEvent>.Raise(new GateToggledEvent(gameObject, _isOpen));
        }

        private IEnumerator Animate(float target)
        {
            if (pivotTransform == null) yield break;
            float start = pivotTransform.localEulerAngles.y;
            float elapsed = 0f;
            while (elapsed < animDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
                pivotTransform.localEulerAngles = new Vector3(0f, Mathf.LerpAngle(start, target, t), 0f);
                yield return null;
            }
            pivotTransform.localEulerAngles = new Vector3(0f, target, 0f);
        }
    }
}
 