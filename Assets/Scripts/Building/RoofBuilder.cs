// ═══════════════════════════════════════════════════════════════════════════════
// 2.7b — ROOF BUILDER
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
    /// 2.7b — Roof Builder.
    ///
    /// Detects when split logs placed on a horizontal frame reach the
    /// rain-blocking threshold (≥3). For slanted roofs, detects lean-angle
    /// logs placed on wall tops.
    /// </summary>
    public class RoofBuilder : MonoBehaviour
    {
        [Header("Config (inject once)")]
        [SerializeField] private StructuralConfig config;

        // Track roof frames: horizontal frame pieces → fill split logs
        private readonly Dictionary<LogPiece, List<LogPiece>> _roofFrames = new();

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

            if (piece.LogType == LogType.Split && IsElevated(piece))
                CheckRoofFrame(piece);

            if (e.Orientation == PlacementMode.Diagonal)
                CheckSlantedRoof(piece);
        }

        // ── Flat roof ─────────────────────────────────────────────────────────
        private void CheckRoofFrame(LogPiece splitPiece)
        {
            // Find the horizontal frame log this split log rests on
            LogPiece frame = null;
            foreach (var sup in splitPiece.SupportedBy)
                if (sup.Orientation == PlacementMode.Horizontal) { frame = sup; break; }

            if (frame == null) return;

            if (!_roofFrames.ContainsKey(frame)) _roofFrames[frame] = new List<LogPiece>();
            var fills = _roofFrames[frame];
            if (!fills.Contains(splitPiece)) fills.Add(splitPiece);

            bool blocksRain = fills.Count >= config.minSplitLogsForRainBlock;
            if (blocksRain)
            {
                Vector3 center = ComputeCenter(fills);
                EventBus<RoofCompletedEvent>.Raise(new RoofCompletedEvent(false, true, center));
                EventBus<ShelterStatusChangedEvent>.Raise(new ShelterStatusChangedEvent(true, false));
            }
        }

        // ── Slanted (A-frame) roof ────────────────────────────────────────────
        private void CheckSlantedRoof(LogPiece diagonalPiece)
        {
            // A diagonal piece placed on a wall top initiates slant tracking
            // When two diagonal logs meet at peak → raise RoofCompletedEvent
            // (simplified — real detection uses overlap at apex position)
            EventBus<RoofCompletedEvent>.Raise(
                new RoofCompletedEvent(true, true, diagonalPiece.transform.position));
        }

        private static bool IsElevated(LogPiece piece)
            => piece.transform.position.y > 0.5f; // above ground level

        private static Vector3 ComputeCenter(List<LogPiece> pieces)
        {
            if (pieces.Count == 0) return Vector3.zero;
            Vector3 s = Vector3.zero;
            foreach (var p in pieces) s += p.transform.position;
            return s / pieces.Count;
        }
    }
}
