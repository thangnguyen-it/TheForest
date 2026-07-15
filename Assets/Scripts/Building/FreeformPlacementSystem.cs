using UnityEngine;
using UnityEngine.InputSystem;
using TheForest.Building.Config;
using TheForest.Building.Core;
using TheForest.Building.Data;
using TheForest.Building.Events;

namespace TheForest.Building.Systems
{
    /// <summary>
    /// 2.3 — Freeform Placement System.
    ///
    /// Manages the full ghost → validate → place pipeline for freeform building:
    ///   • Proximity-based snap (no fixed grid)
    ///   • Right-click cycles placement mode (Horizontal → Vertical → Diagonal)
    ///   • Q/R rotate the piece
    ///   • Left-click places when valid
    ///   • G drops held log to ground
    ///
    /// Communicates OUT: PlacementModeChangedEvent, SnapPointFoundEvent,
    ///                   BuildingPieceDroppedEvent, BuildingPieceAddedEvent,
    ///                   LogPickedUpEvent, LogDroppedEvent, LogPlacedEvent.
    /// Communicates IN:  PlayerSprintStateChangedEvent (block during sprint).
    /// </summary>
    public class FreeformPlacementSystem : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Config (inject once)")]
        [SerializeField] private PlacementConfig placementConfig;
        [SerializeField] private MaterialDatabase materialDB;

        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Transform playerTransform;

        // ── Runtime ──────────────────────────────────────────────────────────
        private PlacementMode _mode = PlacementMode.Horizontal;
        private LogType _heldType;
        private bool _hasHeldLog;
        private bool _isSprinting;
        private float _currentYRotation;

        // Ghost state
        private GameObject _ghostObject;
        private bool _ghostValid;
        private Material _ghostMat;

        // Snap state
        private SnapPoint _currentSnapPoint;
        private Vector3 _ghostPosition;
        private Quaternion _ghostRotation;

        // Overlap check buffer
        private readonly Collider[] _overlapBuf = new Collider[8];

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void Awake()
        {
            if (placementConfig == null) Debug.LogError("[FreeformPlacement] PlacementConfig missing.", this);
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
            if (playerTransform == null)
                playerTransform = transform;
        }

        private void OnEnable()
        {
            EventBus<PlayerSprintStateChangedEvent>.Subscribe(OnSprintChanged);
        }

        private void OnDisable()
        {
            EventBus<PlayerSprintStateChangedEvent>.Unsubscribe(OnSprintChanged);
            DestroyGhost();
        }

        // ── Update ───────────────────────────────────────────────────────────
        private void Update()
        {
            if (!_hasHeldLog) return;
            if (_isSprinting) { HideGhost(); return; }

            UpdateGhostTransform();
            ValidateGhost();
            UpdateGhostVisual();
        }

        // ── Ghost Transform ───────────────────────────────────────────────────
        private void UpdateGhostTransform()
        {
            if (cameraTransform == null || placementConfig == null) return;

            var ray = new Ray(cameraTransform.position, cameraTransform.forward);

            // Prefer snap point
            _currentSnapPoint = FindNearestSnap(ray);
            if (_currentSnapPoint != null)
            {
                _ghostPosition = _currentSnapPoint.transform.position;
                _ghostRotation = _currentSnapPoint.transform.rotation
                                 * Quaternion.Euler(0f, _currentYRotation, 0f);
                EventBus<SnapPointFoundEvent>.Raise(
                    new SnapPointFoundEvent(_ghostPosition, _ghostRotation, _currentSnapPoint.gameObject));
            }
            else if (Physics.Raycast(ray, out RaycastHit hit, placementConfig.maxGroundAngle * 0.1f + 5f,
                                     placementConfig.terrainLayer, QueryTriggerInteraction.Ignore))
            {
                _ghostPosition = hit.point;
                _ghostRotation = ComputeRotationOnSurface(hit.normal);
            }
            else
            {
                // Floating placement
                _ghostPosition = cameraTransform.position + cameraTransform.forward * 3f;
                _ghostRotation = Quaternion.Euler(0f, playerTransform.eulerAngles.y + _currentYRotation, 0f);
            }

            if (_ghostObject != null)
                _ghostObject.transform.SetPositionAndRotation(_ghostPosition, _ghostRotation);
        }

        /// <summary>Compute placement rotation according to current mode and surface normal.</summary>
        private Quaternion ComputeRotationOnSurface(Vector3 surfaceNormal)
        {
            float yaw = playerTransform.eulerAngles.y + _currentYRotation;
            return _mode switch
            {
                PlacementMode.Vertical => Quaternion.Euler(90f, yaw, 0f),
                PlacementMode.Diagonal => Quaternion.Euler(45f, yaw, 0f),
                _ => Quaternion.Euler(0f, yaw, 0f)
            };
        }

        // ── Validation ────────────────────────────────────────────────────────
        private void ValidateGhost()
        {
            if (_ghostObject == null) return;

            // Surface angle
            bool angleOk = true;
            if (Physics.Raycast(_ghostPosition + Vector3.up * 0.5f, Vector3.down, out RaycastHit ground, 2f,
                                 placementConfig.terrainLayer))
            {
                float angle = Vector3.Angle(ground.normal, Vector3.up);
                angleOk = angle <= placementConfig.maxGroundAngle;
            }

            // Overlap check (half-extents based on log type)
            Vector3 halfExtents = GetHalfExtents(_heldType);
            int hits = Physics.OverlapBoxNonAlloc(
                _ghostPosition, halfExtents, _overlapBuf,
                _ghostRotation, placementConfig.obstacleMask,
                QueryTriggerInteraction.Ignore);

            bool overlapOk = (hits == 0);
            _ghostValid = angleOk && overlapOk;

            EventBus<PlacementValidityChangedEvent>.Raise(
                new PlacementValidityChangedEvent(_ghostValid,
                    !_ghostValid ? (angleOk ? "Overlap" : "Slope too steep") : null));
        }

        private Vector3 GetHalfExtents(LogType type) => type switch
        {
            LogType.Full => new Vector3(placementConfig.logRadius, placementConfig.logRadius, placementConfig.fullLogLength * 0.5f),
            LogType.Half => new Vector3(placementConfig.logRadius, placementConfig.logRadius, placementConfig.fullLogLength * 0.25f),
            LogType.Split => new Vector3(placementConfig.fullLogLength * 0.5f, 0.06f, placementConfig.logRadius),
            LogType.Stick => new Vector3(0.04f, 0.04f, placementConfig.fullLogLength * 0.25f),
            _ => new Vector3(placementConfig.logRadius, placementConfig.logRadius, placementConfig.fullLogLength * 0.5f)
        };

        private void UpdateGhostVisual()
        {
            if (_ghostMat == null) return;
            var col = _ghostValid ? placementConfig.ghostValidColor : placementConfig.ghostInvalidColor;
            _ghostMat.color = col;
        }

        // ── Placement ────────────────────────────────────────────────────────
        /// <summary>Called by Input System "Place" action (Left Mouse Button).</summary>
        public void OnPlace(InputValue value)
        {
            if (!value.isPressed || !_hasHeldLog || !_ghostValid) return;
            PlacePiece();
        }

        private void PlacePiece()
        {
            var prefab = materialDB.GetPrefab(LogTypeToId(_heldType));
            if (prefab == null) { Debug.LogWarning("[Freeform] No prefab for " + _heldType); return; }

            var go = Instantiate(prefab, _ghostPosition, _ghostRotation);
            var piece = go.GetComponent<LogPiece>();
            if (piece == null) piece = go.AddComponent<LogPiece>();

            piece.Initialize(_heldType, _mode, GetDefaultHealth(_heldType));

            // Occupy snap point if we snapped
            _currentSnapPoint?.TryOccupy();

            EventBus<LogPlacedEvent>.Raise(
                new LogPlacedEvent(go, _heldType, _ghostPosition, _ghostRotation, 0));
            EventBus<BuildingPieceAddedEvent>.Raise(
                new BuildingPieceAddedEvent(go, _heldType, _mode));

            ClearHeldLog();
        }

        // ── Drop ─────────────────────────────────────────────────────────────
        /// <summary>G key — drop log at player feet.</summary>
        public void OnDropLog(InputValue value)
        {
            if (!value.isPressed || !_hasHeldLog) return;
            Vector3 dropPos = playerTransform.position + playerTransform.forward * 0.8f;
            EventBus<LogDroppedEvent>.Raise(new LogDroppedEvent(_ghostObject, _heldType, dropPos));
            EventBus<BuildingPieceDroppedEvent>.Raise(new BuildingPieceDroppedEvent(_ghostObject, _heldType, dropPos));
            ClearHeldLog();
        }

        // ── Mode cycle (Right Mouse Button) ──────────────────────────────────
        public void OnCyclePlacementMode(InputValue value)
        {
            if (!value.isPressed || !_hasHeldLog) return;
            var old = _mode;
            _mode = (PlacementMode)(((int)_mode + 1) % 3);
            EventBus<PlacementModeChangedEvent>.Raise(new PlacementModeChangedEvent(old, _mode));
        }

        // ── Rotation (Q / R) ─────────────────────────────────────────────────
        public void OnRotateCCW(InputValue v) { if (v.isPressed && _hasHeldLog) _currentYRotation -= placementConfig.rotationStep; }
        public void OnRotateCW(InputValue v) { if (v.isPressed && _hasHeldLog) _currentYRotation += placementConfig.rotationStep; }

        // ── Public API: give log to player ───────────────────────────────────
        /// <summary>
        /// Called by inventory/pickup when player picks up a log.
        /// Creates ghost object immediately.
        /// </summary>
        public void GiveLog(LogType type)
        {
            ClearHeldLog();
            _heldType = type;
            _hasHeldLog = true;
            _currentYRotation = 0f;

            var prefab = materialDB.GetPrefab(LogTypeToId(type));
            if (prefab != null)
            {
                _ghostObject = Instantiate(prefab, Vector3.zero, Quaternion.identity);
                _ghostMat = CreateGhostMaterial(_ghostObject);
            }

            EventBus<LogPickedUpEvent>.Raise(new LogPickedUpEvent(_ghostObject, type));
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private SnapPoint FindNearestSnap(Ray ray)
        {
            if (!Physics.Raycast(ray, out RaycastHit hit, 5f, placementConfig.obstacleMask)) return null;

            var buf = new Collider[8];
            int count = Physics.OverlapSphereNonAlloc(hit.point, placementConfig.snapRadius,
                buf, placementConfig.obstacleMask, QueryTriggerInteraction.Collide);

            SnapPoint best = null;
            float bestSqr = placementConfig.snapRadius * placementConfig.snapRadius;
            for (int i = 0; i < count; i++)
            {
                var sp = buf[i].GetComponent<SnapPoint>();
                if (sp == null || sp.IsOccupied || sp.AllowedMode != _mode) continue;
                float d = (sp.transform.position - hit.point).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = sp; }
            }
            return best;
        }

        private Material CreateGhostMaterial(GameObject ghost)
        {
            var r = ghost.GetComponentInChildren<Renderer>();
            if (r == null) return null;
            var mat = new Material(r.sharedMaterial);
            r.material = mat;
            return mat;
        }

        private void HideGhost() { if (_ghostObject) _ghostObject.SetActive(false); }

        private void ClearHeldLog()
        {
            DestroyGhost();
            _hasHeldLog = false;
            _currentSnapPoint = null;
        }

        private void DestroyGhost()
        {
            if (_ghostObject != null) Destroy(_ghostObject);
            _ghostObject = null;
            _ghostMat = null;
        }

        private void OnSprintChanged(PlayerSprintStateChangedEvent e) => _isSprinting = e.IsSprinting;

        private static float GetDefaultHealth(LogType t) => t switch
        {
            LogType.Full => 100f,
            LogType.Split => 55f,
            LogType.Stick => 25f,
            LogType.Stone => 220f,
            _ => 80f
        };

        private static string LogTypeToId(LogType t) => t switch
        {
            LogType.Full => "full_log",
            LogType.ThreeQuarter => "three_quarter_log",
            LogType.Half => "half_log",
            LogType.Quarter => "quarter_log",
            LogType.Split => "split_log",
            LogType.SplitQuarter => "split_quarter",
            LogType.Stick => "stick",
            LogType.Stone => "large_stone",
            _ => "full_log"
        };
    }
}