// ═══════════════════════════════════════════════════════════════════════════════
// 2.6 — STRUCTURAL DEPENDENCY GRAPH
// ═══════════════════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;
using TheForest.Building.Config;
using TheForest.Building.Core;
using TheForest.Building.Data;
using TheForest.Building.Events;

namespace TheForest.Building.Systems
{
    /// <summary>
    /// 2.6 — Structural Integrity &amp; Dependency Graph.
    ///
    /// Maintains a directed graph: each LogPiece knows what it rests on (SupportedBy)
    /// and what rests on it (Supporting).
    ///
    /// Dismantle validation: CanDismantle() iff Supporting.Count == 0.
    ///
    /// Strut system:
    ///   When two struts are placed at both corners of a horizontal beam resting on
    ///   two vertical columns, the middle columns' dependency on the beam is released —
    ///   they can be removed. After removal the strut region is marked Immutable.
    ///
    /// Max strut span: 5 log-units (config-driven).
    /// Communicates IN:  BuildingPieceAddedEvent, BuildingPieceDismantledEvent.
    /// Communicates OUT: StrutPlacedEvent, PieceImmutableEvent.
    /// </summary>
    public class StructuralDependencyGraph : MonoBehaviour
    {
        [Header("Config (inject once)")]
        [SerializeField] private StructuralConfig config;

        // Main graph storage
        private readonly Dictionary<LogPiece, HashSet<LogPiece>> _graph = new();

        // Strut tracking: keyed by horizontal beam piece
        private readonly Dictionary<LogPiece, StrutPair> _strutPairs = new();

        private readonly Collider[] _supportBuf = new Collider[8];

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void Awake()
        {
            if (config == null) Debug.LogError("[DependencyGraph] StructuralConfig missing.", this);
        }

        private void OnEnable()
        {
            EventBus<BuildingPieceAddedEvent>.Subscribe(OnPieceAdded);
            EventBus<BuildingPieceDismantledEvent>.Subscribe(OnPieceDismantled);
        }

        private void OnDisable()
        {
            EventBus<BuildingPieceAddedEvent>.Unsubscribe(OnPieceAdded);
            EventBus<BuildingPieceDismantledEvent>.Unsubscribe(OnPieceDismantled);
        }

        // ── Event handlers ───────────────────────────────────────────────────
        private void OnPieceAdded( BuildingPieceAddedEvent e)
        {
            var piece = e.Piece?.GetComponent<LogPiece>();
            if (piece == null) return;

            RegisterPiece(piece);
            DetectSupport(piece);
        }

        private void OnPieceDismantled(BuildingPieceDismantledEvent e)
        {
            // The actual removal happens via RepairDismantleSystem.
            // This graph reacts when a piece is physically destroyed.
        }

        // ── Registration ─────────────────────────────────────────────────────
        private void RegisterPiece(LogPiece piece)
        {
            if (!_graph.ContainsKey(piece))
                _graph[piece] = new HashSet<LogPiece>();
        }

        public void UnregisterPiece(LogPiece piece)
        {
            if (!_graph.ContainsKey(piece)) return;

            // Remove from all dependency relationships
            foreach (var kv in _graph)
            {
                kv.Value.Remove(piece);
                if (kv.Key != piece)
                {
                    kv.Key.RemoveSupportedBy(piece);
                    kv.Key.RemoveSupporting(piece);
                }
            }

            _graph.Remove(piece);
        }

        // ── Support detection ─────────────────────────────────────────────────
        /// <summary>
        /// Detects what pieces are directly below a newly placed piece and
        /// registers the support relationship bidirectionally.
        /// </summary>
        private void DetectSupport(LogPiece piece)
        {
            Vector3 checkOrigin = piece.transform.position + Vector3.down * (0.05f);
            int count = Physics.OverlapSphereNonAlloc(
                checkOrigin, config.supportDetectionRadius,
                _supportBuf, config.buildingPieceLayer,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                var supporter = _supportBuf[i].GetComponentInParent<LogPiece>();
                if (supporter == null || supporter == piece) continue;

                RegisterPiece(supporter);

                // Register support relationship
                piece.AddSupportedBy(supporter);
                supporter.AddSupporting(piece);

                if (!_graph[supporter].Contains(piece))
                    _graph[supporter].Add(piece);
            }
        }

        // ── Public validation API ─────────────────────────────────────────────
        /// <summary>Returns true when piece can be safely dismantled.</summary>
        public bool CanDismantle(LogPiece piece) => piece != null && piece.CanDismantle;

        // ── Strut system ──────────────────────────────────────────────────────
        /// <summary>
        /// Called by DoorWindowSystem / FreeformPlacementSystem when a split-log
        /// strut is placed at a corner joint (L-shape indicator).
        ///
        /// Algorithm:
        ///   1. Identify the horizontal beam and the two vertical columns at its ends.
        ///   2. If both strut corners are now filled → find middle columns.
        ///   3. Release their SupportedBy dependency on the beam.
        ///   4. Mark the strut region Immutable.
        /// </summary>
        public void RegisterStrut(LogPiece strutPiece, LogPiece cornerVertical, LogPiece horizontalBeam)
        {
            if (!_strutPairs.ContainsKey(horizontalBeam))
                _strutPairs[horizontalBeam] = new StrutPair();

            var pair = _strutPairs[horizontalBeam];
            pair.Register(strutPiece, cornerVertical);

            EventBus<StrutPlacedEvent>.Raise(
                new StrutPlacedEvent(strutPiece.transform.position,
                    pair.StrutA?.gameObject, pair.StrutB?.gameObject));

            if (pair.IsComplete)
                ApplyStrutSpan(horizontalBeam, pair);
        }

        private void ApplyStrutSpan(LogPiece beam, StrutPair pair)
        {
            // Find middle columns inside the span (up to maxColumnsPerStrut)
            float spanLength = Vector3.Distance(
                pair.ColumnA.transform.position, pair.ColumnB.transform.position);

            if (spanLength > config.maxStrutSpan * config.supportDetectionRadius * 2f)
            {
                Debug.LogWarning("[DependencyGraph] Span exceeds maxStrutSpan — strut rejected.");
                return;
            }

            // Detach middle columns: find verticals between the two corner columns
            foreach (var kv in _graph)
            {
                var col = kv.Key;
                if (col == pair.ColumnA || col == pair.ColumnB) continue;
                if (col.Orientation != PlacementMode.Vertical) continue;

                if (IsPointBetween(col.transform.position, pair.ColumnA.transform.position,
                                                           pair.ColumnB.transform.position,
                                   config.supportDetectionRadius))
                {
                    col.RemoveSupportedBy(beam);
                    beam.RemoveSupporting(col);
                }
            }

            // Mark strut pieces and the beam as immutable
            beam.SetImmutable();
            pair.StrutA?.SetImmutable();
            pair.StrutB?.SetImmutable();
        }

        private static bool IsPointBetween(Vector3 point, Vector3 a, Vector3 b, float tolerance)
        {
            Vector3 ab = b - a;
            Vector3 ap = point - a;
            float len = ab.magnitude;
            if (len < 0.001f) return false;
            float t = Vector3.Dot(ap, ab) / (len * len);
            return t > 0.05f && t < 0.95f &&
                   Vector3.Cross(ab.normalized, ap).magnitude < tolerance;
        }

        // ── Inner type ────────────────────────────────────────────────────────
        private class StrutPair
        {
            public LogPiece StrutA;
            public LogPiece StrutB;
            public LogPiece ColumnA;
            public LogPiece ColumnB;
            public bool IsComplete => StrutA != null && StrutB != null;

            public void Register(LogPiece strut, LogPiece column)
            {
                if (StrutA == null) { StrutA = strut; ColumnA = column; }
                else { StrutB = strut; ColumnB = column; }
            }
        }
    }
}
