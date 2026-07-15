using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using TheForest.Building.Config;
using TheForest.Building.Core;
using TheForest.Building.Data;
using TheForest.Building.Events;

namespace TheForest.Building.Systems
{
    /// <summary>
    /// 2.1 — Diegetic Indicator System.
    ///
    /// Spawns/repositions world-space indicator objects that conform to surfaces using
    /// HDRP DecalProjector (flat indicators) or mesh GameObjects (directional arrows).
    ///
    /// Logic pipeline each tick:
    ///   Raycast → detect context → choose IndicatorType → spawn/move/hide prefab.
    ///
    /// Communicates OUT:  IndicatorTypeChangedEvent, PlacementValidityChangedEvent.
    /// Communicates IN:   LogPickedUpEvent, LogDroppedEvent (track held item).
    /// Never calls any other system directly.
    /// </summary>
    public class PlacementIndicatorController : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Config (inject once in Awake)")]
        [SerializeField] private IndicatorConfig config;

        [Header("Camera")]
        [SerializeField] private Transform cameraTransform;

        // ── Runtime state ────────────────────────────────────────────────────
        private LogType _heldLogType;
        private bool _holdingLog;
        private bool _holdingAxe;
        private PlacementMode _currentMode = PlacementMode.Horizontal;

        private IndicatorType _lastIndicatorType = IndicatorType.None;
        private bool _lastValidity = true;

        // Pool: one active GameObject per IndicatorType
        private readonly Dictionary<IndicatorType, GameObject> _pool = new();
        private GameObject _activeIndicator;

        private float _updateInterval;
        private float _updateTimer;

        // Snap scan buffer
        private readonly Collider[] _overlapBuffer = new Collider[16];

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void Awake()
        {
            if (config == null)
                Debug.LogError("[PlacementIndicator] IndicatorConfig not assigned.", this);

            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            _updateInterval = config ? 1f / Mathf.Max(1f, config.updateRate) : 1f / 30f;
        }

        private void OnEnable()
        {
            EventBus<LogPickedUpEvent>.Subscribe(OnLogPickedUp);
            EventBus<LogDroppedEvent>.Subscribe(OnLogDropped);
            EventBus<PlacementModeChangedEvent>.Subscribe(OnModeChanged);
        }

        private void OnDisable()
        {
            EventBus<LogPickedUpEvent>.Unsubscribe(OnLogPickedUp);
            EventBus<LogDroppedEvent>.Unsubscribe(OnLogDropped);
            EventBus<PlacementModeChangedEvent>.Unsubscribe(OnModeChanged);
            HideAll();
        }

        // ── Update ────────────────────────────────────────────────────────────
        private void Update()
        {
            _updateTimer -= Time.deltaTime;
            if (_updateTimer > 0f) return;
            _updateTimer = _updateInterval;
            Tick();
        }

        // ── Core Tick ─────────────────────────────────────────────────────────
        private void Tick()
        {
            if (cameraTransform == null || config == null) return;

            var ray = new Ray(cameraTransform.position, cameraTransform.forward);

            if (!Physics.Raycast(ray, out RaycastHit hit, config.maxRaycastDistance,
                                  config.groundLayer | config.logLayer,
                                  QueryTriggerInteraction.Ignore))
            {
                HideAll();
                BroadcastValidity(false, "Nothing in range");
                return;
            }

            // Determine context
            var hitPiece = hit.collider.GetComponentInParent<LogPiece>();
            var snapPoint = FindNearestSnapPoint(hit.point, config.snapDetectionRadius);
            IndicatorType type;
            Vector3 indicatorPos = hit.point;
            Quaternion indicatorRot = Quaternion.LookRotation(hit.normal);

            if (_holdingAxe && hitPiece != null)
            {
                type = DetermineAxeIndicator(hitPiece, hit);
            }
            else if (_holdingLog)
            {
                type = DetermineLogIndicator(hitPiece, snapPoint, hit, out indicatorPos, out indicatorRot);
            }
            else
            {
                HideAll();
                return;
            }

            ShowIndicator(type, indicatorPos, indicatorRot);
            BroadcastType(type, indicatorPos, hit.normal);
        }

        // ── Axe indicator logic ───────────────────────────────────────────────
        /// <summary>
        /// Determine cut indicator type based on the angle between camera forward
        /// and the log's long axis.
        ///  &lt;15°  → looking along axis → split mark
        ///  &gt;75°  → looking at end      → sharpen spike
        ///  else   → looking at side     → width cut
        /// </summary>
        private IndicatorType DetermineAxeIndicator(LogPiece piece, RaycastHit hit)
        {
            Vector3 logAxis = piece.transform.forward; // log long axis
            Vector3 camForward = cameraTransform.forward;
            float angle = Vector3.Angle(camForward, logAxis);

            // Looking at end-cap → sharpen
            if (angle < 15f || angle > 165f)
            {
                // Only valid on vertical logs (palisade)
                return piece.Orientation == PlacementMode.Vertical
                    ? IndicatorType.SpikeIndicator
                    : IndicatorType.CutMarkWidth;
            }

            // Looking along the length → split
            if (angle < 75f || angle > 105f)
                return IndicatorType.CutMarkSplit;

            // Structural logs carrying load cannot be cut
            if (piece.Supporting.Count > 0)
            {
                BroadcastValidity(false, "Log is load-bearing");
                return IndicatorType.InvalidPlacement;
            }

            return IndicatorType.CutMarkWidth;
        }

        // ── Log placement indicator logic ─────────────────────────────────────
        private IndicatorType DetermineLogIndicator(
            LogPiece hitPiece, SnapPoint nearestSnap, RaycastHit hit,
            out Vector3 pos, out Quaternion rot)
        {
            pos = hit.point;
            rot = Quaternion.LookRotation(hit.normal);

            // Near a snap point → show snap arrow aligned to snap
            if (nearestSnap != null)
            {
                pos = nearestSnap.transform.position;
                rot = nearestSnap.transform.rotation;
                return IndicatorType.SnapArrow;
            }

            // Hit an existing vertical log → connection dash (will form wall)
            if (hitPiece != null && hitPiece.Orientation == PlacementMode.Vertical)
            {
                rot = Quaternion.LookRotation(hitPiece.transform.right);
                return IndicatorType.ConnectionDash;
            }

            // Mode-based defaults
            return _currentMode switch
            {
                PlacementMode.Vertical => IndicatorType.VerticalLog,
                PlacementMode.Diagonal => IndicatorType.DiagonalLog,
                _ => IndicatorType.HorizontalLog
            };
        }

        // ── Snap scan ────────────────────────────────────────────────────────
        private SnapPoint FindNearestSnapPoint(Vector3 origin, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(origin, radius,
                _overlapBuffer, config.logLayer, QueryTriggerInteraction.Collide);

            SnapPoint best = null;
            float bestDistSq = radius * radius;

            for (int i = 0; i < count; i++)
            {
                var sp = _overlapBuffer[i].GetComponent<SnapPoint>();
                if (sp == null || sp.IsOccupied) continue;
                float d = (sp.transform.position - origin).sqrMagnitude;
                if (d < bestDistSq) { bestDistSq = d; best = sp; }
            }
            return best;
        }

        // ── Indicator pooling & display ───────────────────────────────────────
        private void ShowIndicator(IndicatorType type, Vector3 pos, Quaternion rot)
        {
            // Hide previous if type changed
            if (_activeIndicator != null && type != _lastIndicatorType)
                _activeIndicator.SetActive(false);

            _activeIndicator = GetOrCreateIndicator(type);
            if (_activeIndicator == null) return;

            _activeIndicator.transform.SetPositionAndRotation(pos, rot);
            _activeIndicator.SetActive(true);

            _lastIndicatorType = type;
            BroadcastValidity(type != IndicatorType.InvalidPlacement, null);
        }

        private void HideAll()
        {
            foreach (var kv in _pool) kv.Value.SetActive(false);
            _activeIndicator = null;
        }

        private GameObject GetOrCreateIndicator(IndicatorType type)
        {
            if (_pool.TryGetValue(type, out var existing)) return existing;

            var prefab = GetPrefabForType(type);
            if (prefab == null) return null;

            var go = Instantiate(prefab);
            go.SetActive(false);
            _pool[type] = go;
            return go;
        }

        private GameObject GetPrefabForType(IndicatorType type) => type switch
        {
            IndicatorType.HorizontalLog => config.horizontalPrefab,
            IndicatorType.VerticalLog => config.verticalPrefab,
            IndicatorType.SnapArrow => config.snapArrowPrefab,
            IndicatorType.ConnectionDash => config.connectionDashPrefab,
            IndicatorType.DiagonalLog => config.diagonalArrowPrefab,
            IndicatorType.CutMarkWidth => config.cutMarkWidthPrefab,
            IndicatorType.CutMarkSplit => config.cutMarkSplitPrefab,
            IndicatorType.InvalidPlacement => config.invalidPrefab,
            IndicatorType.StrutIndicator => config.strutPrefab,
            IndicatorType.StairsIndicator => config.stairsPrefab,
            IndicatorType.DoorCutMark => config.doorCutPrefab,
            IndicatorType.SpikeIndicator => null, // handled by spike tip mesh
            _ => null
        };

        // ── Event broadcasting ───────────────────────────────────────────────
        private void BroadcastType(IndicatorType type, Vector3 pos, Vector3 normal)
        {
            if (type == _lastIndicatorType) return;
            EventBus<IndicatorTypeChangedEvent>.Raise(
                new IndicatorTypeChangedEvent(type, pos, normal, _currentMode));
        }

        private void BroadcastValidity(bool valid, string reason)
        {
            if (valid == _lastValidity) return;
            _lastValidity = valid;
            EventBus<PlacementValidityChangedEvent>.Raise(
                new PlacementValidityChangedEvent(valid, reason));
        }

        // ── Event handlers (EventBus IN) ──────────────────────────────────────
        private void OnLogPickedUp( LogPickedUpEvent e)
        {
            _heldLogType = e.LogType;
            _holdingLog = true;
            _holdingAxe = false;
        }

        private void OnLogDropped( LogDroppedEvent e)
        {
            _holdingLog = false;
            HideAll();
        }

        private void OnModeChanged( PlacementModeChangedEvent e) => _currentMode = e.NewMode;

        // ── Axe state (set by EquipmentController listener) ──────────────────
        /// <summary>Called externally when player equips/unequips an axe.</summary>
        public void SetHoldingAxe(bool holding)
        {
            _holdingAxe = holding;
            if (holding) _holdingLog = false;
        }
    }
}