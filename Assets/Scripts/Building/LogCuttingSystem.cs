// ═══════════════════════════════════════════════════════════════════════════════
// 2.4 — LOG CUTTING SYSTEM
// ═══════════════════════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.InputSystem;
using TheForest.Building.Config;
using TheForest.Building.Core;
using TheForest.Building.Data;
using TheForest.Building.Events;

namespace TheForest.Building.Systems
{
    /// <summary>
    /// 2.4 — Log Cutting System.
    ///
    /// Three cut types:
    ///   Width  — perpendicular: produces two shorter logs (3/4 + 1/4, 1/2 + 1/2, 1/4 + 3/4)
    ///   Length — split/hotdog:  produces two Split planks
    ///   Sharpen — top-of-vertical: converts to spike (handled by LogPiece.SetSpiked)
    ///
    /// Cut sequence: Left-click primes the cut; second Left-click confirms.
    /// Indicator is driven by PlacementIndicatorController listening to the
    /// IndicatorTypeChangedEvent. This system only raises CutAttemptedEvent /
    /// CutCompletedEvent / LogSharpenedEvent.
    ///
    /// FIX (đối chiếu Phần A.3 — báo cáo roadmap): ExecuteWidthCut/ExecuteSplitCut trước đây luôn
    /// raise `new LogCutEvent(null, ...)` — DoorWindowSystem.OnLogCut() đọc
    /// `e.OriginalLog?.transform.position ?? Vector3.zero` nên LUÔN nhận cutPos = (0,0,0) bất kể
    /// người chơi cắt log ở đâu, khiến CheckForDoorOpening() không bao giờ dò đúng cột log vừa cắt.
    /// Sửa: giữ tham chiếu gameObject gốc TRƯỚC khi Destroy, raise event trước khi hủy (không phụ
    /// thuộc "may mà Destroy() chưa dọn object ngay trong cùng frame" — rõ ràng và an toàn hơn).
    /// </summary>
    public class LogCuttingSystem : MonoBehaviour
    {
        [Header("Config (inject once)")]
        [SerializeField] private PlacementConfig config;
        [SerializeField] private MaterialDatabase materialDB;

        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        // Axe equip state — set externally
        private bool _holdingAxe;

        // Cut state machine
        private LogPiece _primed;          // first click confirms target
        private CutActionType _primedCutType;
        private float _cutPositionT;    // 0..1 along log length (width cuts)

        private const float CutRayDistance = 4f;
        private const float CutAngleSplit = 30f;  // degrees: camera along log axis
        private const float CutAngleSharpen = 20f;  // degrees: camera facing log end

        private void Awake()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        private void OnEnable() { }
        private void OnDisable() { _primed = null; }

        // ── Called by EquipmentController listener ────────────────────────────
        public void SetHoldingAxe(bool holding)
        {
            _holdingAxe = holding;
            if (!holding) _primed = null;
        }

        // ── Input (Left Mouse Button) ─────────────────────────────────────────
        public void OnCutAction(InputValue value)
        {
            if (!value.isPressed || !_holdingAxe) return;

            if (_primed == null)
                TryPrimeCut();
            else
                ExecuteCut();
        }

        // ── PRIME: first click — identify target and cut type ─────────────────
        private void TryPrimeCut()
        {
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, CutRayDistance,
                                  ~0, QueryTriggerInteraction.Ignore)) return;

            var piece = hit.collider.GetComponentInParent<LogPiece>();
            if (piece == null) return;

            // Structural pieces carrying load cannot be width-cut (GDD rule)
            if (piece.Supporting.Count > 0 && !IsLengthCut(piece))
            {
                Debug.Log("[Cut] Log is load-bearing — cannot cut.");
                return;
            }

            _primedCutType = DetermineCutType(piece, hit.point);
            _cutPositionT = ComputeCutPositionT(piece, hit.point);
            _primed = piece;

            EventBus<CutAttemptedEvent>.Raise(new CutAttemptedEvent(piece.gameObject, _primedCutType));
        }

        // ── EXECUTE: second click — produce result pieces ─────────────────────
        private void ExecuteCut()
        {
            if (_primed == null) return;
            var piece = _primed;
            _primed = null;

            switch (_primedCutType)
            {
                case CutActionType.Width: ExecuteWidthCut(piece); break;
                case CutActionType.Length: ExecuteSplitCut(piece); break;
                case CutActionType.Sharpen: ExecuteSharpen(piece); break;
            }
        }

        // ── Width cut ─────────────────────────────────────────────────────────
        /// <summary>
        /// Cut at 1/4, 1/2, or 3/4 of the log's length.
        /// Snaps _cutPositionT to nearest quarter mark.
        /// </summary>
        private void ExecuteWidthCut(LogPiece piece)
        {
            // Snap to nearest quarter
            float snapped = Mathf.Round(_cutPositionT * 4f) / 4f;
            snapped = Mathf.Clamp(snapped, 0.25f, 0.75f);

            var (typeA, typeB) = WidthCutResult(piece.LogType, snapped);
            Vector3 pos = piece.transform.position;
            Quaternion rot = piece.transform.rotation;
            float fullLen = config.fullLogLength;

            // FIX: giữ tham chiếu THẬT trước khi Destroy, để LogCutEvent mang đúng vị trí cắt.
            GameObject originalGo = piece.gameObject;

            // Piece A — from origin to cut point
            SpawnCutPiece(typeA, pos, rot, 0f, snapped, fullLen);
            // Piece B — from cut point to end
            SpawnCutPiece(typeB, pos, rot, snapped, 1f, fullLen);

            // FIX: raise events TRƯỚC khi Destroy, truyền originalGo thay vì null.
            EventBus<CutCompletedEvent>.Raise(new CutCompletedEvent(typeA, typeB, pos));
            EventBus<LogCutEvent>.Raise(new LogCutEvent(originalGo, typeA, typeB, CutActionType.Width));

            Destroy(originalGo);
        }

        private void SpawnCutPiece(LogType type, Vector3 originPos, Quaternion rot,
                                   float tStart, float tEnd, float fullLen)
        {
            float segLen = (tEnd - tStart) * fullLen;
            float centerT = (tStart + tEnd) * 0.5f;
            Vector3 center = originPos + rot * Vector3.forward * (centerT * fullLen - fullLen * 0.5f);

            var prefab = materialDB.GetPrefab(TypeToId(type));
            if (prefab == null) return;

            var go = Instantiate(prefab, center, rot);
            go.transform.localScale = Vector3.one; // scaled by mesh LOD variant
            var p = go.GetComponent<LogPiece>() ?? go.AddComponent<LogPiece>();
            p.Initialize(type, PlacementMode.Horizontal, 100f);
        }

        // ── Split cut ─────────────────────────────────────────────────────────
        private void ExecuteSplitCut(LogPiece piece)
        {
            var pos = piece.transform.position;
            var rot = piece.transform.rotation;
            GameObject originalGo = piece.gameObject; // FIX: xem chú thích ở ExecuteWidthCut

            // Two planks offset left/right by half logRadius
            for (int side = -1; side <= 1; side += 2)
            {
                var prefab = materialDB.GetPrefab("split_log");
                if (prefab == null) continue;

                Vector3 offset = rot * Vector3.right * (config.logRadius * side * 0.5f);
                var go = Instantiate(prefab, pos + offset, rot);
                var p = go.GetComponent<LogPiece>() ?? go.AddComponent<LogPiece>();
                p.Initialize(LogType.Split, piece.Orientation, 55f);
            }

            // Throw off-cut piece (GDD: mảnh thừa bị ném ra xa)
            ThrowScrap(pos, rot);

            // FIX: raise trước khi Destroy, truyền originalGo thay vì null.
            EventBus<CutCompletedEvent>.Raise(new CutCompletedEvent(LogType.Split, LogType.Split, pos));
            EventBus<LogCutEvent>.Raise(new LogCutEvent(originalGo, LogType.Split, LogType.Split, CutActionType.Length));

            Destroy(originalGo);
        }

        private void ThrowScrap(Vector3 origin, Quaternion rot)
        {
            // Instantiate a small debris piece and apply impulse
            var prefab = materialDB.GetPrefab("quarter_log");
            if (prefab == null) return;
            var go = Instantiate(prefab, origin + Vector3.up * 0.3f, rot);
            var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
            rb.AddForce((rot * Vector3.right + Vector3.up) * 4f, ForceMode.Impulse);
            Destroy(go, 20f);
        }

        // ── Sharpen ───────────────────────────────────────────────────────────
        private void ExecuteSharpen(LogPiece piece)
        {
            if (piece.Orientation != PlacementMode.Vertical)
            {
                Debug.Log("[Cut] Sharpen only works on vertical logs.");
                return;
            }
            piece.SetSpiked();
            EventBus<LogSharpenedEvent>.Raise(new LogSharpenedEvent(piece.gameObject, piece.transform.position));
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private CutActionType DetermineCutType(LogPiece piece, Vector3 hitPoint)
        {
            Vector3 logAxis = piece.transform.forward;
            float angle = Vector3.Angle(cameraTransform.forward, logAxis);

            if (angle < CutAngleSharpen || angle > 180f - CutAngleSharpen)
                return piece.Orientation == PlacementMode.Vertical
                    ? CutActionType.Sharpen : CutActionType.Width;

            if (angle < CutAngleSplit || angle > 180f - CutAngleSplit)
                return CutActionType.Length;

            return CutActionType.Width;
        }

        private bool IsLengthCut(LogPiece piece)
        {
            float angle = Vector3.Angle(cameraTransform.forward, piece.transform.forward);
            return angle < CutAngleSplit || angle > 180f - CutAngleSplit;
        }

        /// <summary>Returns 0..1 position along log where cut will occur.</summary>
        private float ComputeCutPositionT(LogPiece piece, Vector3 worldHitPoint)
        {
            Vector3 localHit = piece.transform.InverseTransformPoint(worldHitPoint);
            float halfLen = config.fullLogLength * 0.5f;
            return Mathf.InverseLerp(-halfLen, halfLen, localHit.z);
        }

        private (LogType, LogType) WidthCutResult(LogType orig, float t)
        {
            if (t <= 0.25f + 0.01f) return (LogType.Quarter, LogType.ThreeQuarter);
            if (t <= 0.50f + 0.01f) return (LogType.Half, LogType.Half);
            return (LogType.ThreeQuarter, LogType.Quarter);
        }

        private static string TypeToId(LogType t) => t switch
        {
            LogType.ThreeQuarter => "three_quarter_log",
            LogType.Half => "half_log",
            LogType.Quarter => "quarter_log",
            LogType.Split => "split_log",
            _ => "full_log"
        };
    }
}
