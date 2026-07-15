// ═══════════════════════════════════════════════════════════════════════════════
// 2.7 — WALL BUILDER
// ═══════════════════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using TheForest.Building.Config;
using TheForest.Building.Core;
using TheForest.Building.Data;
using TheForest.Building.Events;
using UnityEngine;

namespace TheForest.Building.Systems
{
    /// <summary>
    /// 2.7a — Wall Builder.
    ///
    /// Listens to LogPlacedEvent and groups horizontal/vertical logs into walls.
    /// A wall segment is complete when the snap chain reaches standardWallHeight.
    /// Palisade mode: vertical logs that snap into a horizontal row auto-chain.
    /// </summary>
    public class WallBuilder : MonoBehaviour
    {
        [Header("Config (inject once)")]
        [SerializeField] private StructuralConfig config;

        // Track wall segments: root piece → ordered list of pieces
        private readonly Dictionary<LogPiece, List<LogPiece>> _wallSegments = new();

        // FIX (Block 7): palisade row giờ là danh sách các NHÓM gom theo khoảng cách thực tế, không còn
        // Dictionary<LogPiece, List<LogPiece>> khoá bằng "root giả" (bản cũ FindPalisadeRowRoot trả về
        // chính piece nó -> mỗi log tự làm root của chính mình -> KHÔNG BAO GIỜ gom được >=3 log/hàng).
        private readonly List<List<LogPiece>> _palisadeRowGroups = new();

        [Header("Palisade Row Detection")]
        [Tooltip("Khoảng cách tối đa giữa 2 log dọc để coi là CÙNG một hàng cọc. Nên tham chiếu " +
                 "PlacementConfig.palisadeSpacing (mặc định 0.42) và để dư ~1.4-1.5x cho sai số đặt.")]
        [SerializeField] private float palisadeRowJoinDistance = 0.6f;

        private void OnEnable()
        {
            EventBus<BuildingPieceAddedEvent>.Subscribe(OnPieceAdded);
        }

        private void OnDisable()
        {
            EventBus<BuildingPieceAddedEvent>.Unsubscribe(OnPieceAdded);
        }

        private void OnPieceAdded( BuildingPieceAddedEvent e)
        {
            var piece = e.Piece?.GetComponent<LogPiece>();
            if (piece == null) return;

            switch (e.Orientation)
            {
                case PlacementMode.Horizontal: CheckHorizontalWall(piece); break;
                case PlacementMode.Vertical: CheckPalisadeChain(piece); break;
            }
        }

        // ── Standard horizontal wall ──────────────────────────────────────────
        private void CheckHorizontalWall(LogPiece piece)
        {
            // Walk upward through SupportedBy chain to find the base log
            var root = FindWallRoot(piece);
            if (!_wallSegments.ContainsKey(root)) _wallSegments[root] = new List<LogPiece>();

            var segment = _wallSegments[root];
            if (!segment.Contains(piece)) segment.Add(piece);

            if (segment.Count >= config.standardWallHeight)
            {
                Vector3 center = ComputeCenter(segment);
                EventBus<WallCompletedEvent>.Raise(
                    new WallCompletedEvent(segment.Count, false, center));
                CheckRainBlock(segment);
            }
        }

        // ── Palisade chain ────────────────────────────────────────────────────
        private void CheckPalisadeChain(LogPiece piece)
        {
            var row = FindOrCreatePalisadeRow(piece);
            if (!row.Contains(piece)) row.Add(piece);

            if (row.Count >= 3) // Minimum for a meaningful palisade segment
            {
                Vector3 center = ComputeCenter(row);
                EventBus<WallCompletedEvent>.Raise(
                    new WallCompletedEvent(row.Count, true, center));
            }
        }

        /// <summary>
        /// FIX (Block 7): gom log dọc (palisade) thành hàng theo LAN KHOẢNG CÁCH thực tế — nếu log mới
        /// nằm trong palisadeRowJoinDistance của BẤT KỲ log nào đã có sẵn trong 1 hàng, nó nhập hàng đó
        /// (chuỗi lan, không yêu cầu thẳng hàng tuyệt đối theo 1 trục). Không khớp hàng nào -> tạo hàng mới.
        /// Đồng thời dọn các hàng đã rỗng hoàn toàn (log bị phá huỷ) mỗi lần gọi, tránh rò rỉ bộ nhớ.
        /// </summary>
        private List<LogPiece> FindOrCreatePalisadeRow(LogPiece piece)
        {
            _palisadeRowGroups.RemoveAll(row =>
            {
                row.RemoveAll(p => p == null);
                return row.Count == 0;
            });

            Vector3 pos = piece.transform.position;
            foreach (var row in _palisadeRowGroups)
            {
                foreach (var existing in row)
                {
                    if (existing == null) continue;
                    if (Vector3.Distance(existing.transform.position, pos) <= palisadeRowJoinDistance)
                        return row;
                }
            }

            var newRow = new List<LogPiece>();
            _palisadeRowGroups.Add(newRow);
            return newRow;
        }

        // ── Rain blocking ──────────────────────────────────────────────────────
        private void CheckRainBlock(List<LogPiece> pieces)
        {
            int splitCount = 0;
            foreach (var p in pieces)
                if (p.LogType == LogType.Split || p.LogType == LogType.SplitQuarter)
                    splitCount++;

            bool blocked = splitCount >= config.minSplitLogsForRainBlock;
            EventBus<RainBlockedEvent>.Raise(new RainBlockedEvent(blocked, splitCount));
            if (blocked)
                EventBus<ShelterStatusChangedEvent>.Raise(new ShelterStatusChangedEvent(true, true));
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static LogPiece FindWallRoot(LogPiece piece)
        {
            var current = piece;
            while (true)
            {
                LogPiece below = null;
                foreach (var s in current.SupportedBy)
                    if (s.Orientation == PlacementMode.Horizontal)
                    { below = s; break; }
                if (below == null) return current;
                current = below;
            }
        }

        private static Vector3 ComputeCenter(List<LogPiece> pieces)
        {
            if (pieces.Count == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            foreach (var p in pieces) sum += p.transform.position;
            return sum / pieces.Count;
        }
    }
}