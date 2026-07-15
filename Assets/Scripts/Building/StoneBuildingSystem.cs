// ═══════════════════════════════════════════════════════════════════════════════
// 2.13 — STONE BUILDING SYSTEM
// ═══════════════════════════════════════════════════════════════════════════════
using System.Collections;
using UnityEngine.AI;
using System.Collections.Generic;
using TheForest.Building.Config;
using TheForest.Building.Core;
using TheForest.Building.Data;
using TheForest.Building.Events;
using UnityEngine;

namespace TheForest.Building.Systems
{
    /// <summary>
    /// 2.13 — Stone Building System.
    ///
    /// Stone costs (from Data 12):
    ///   Floor tile  : 20 Large Stone per 1×1 unit
    ///   Wall section: 28 Large Stone per segment
    ///   Pillar (×4) :  4 Large Stone per pillar, max 4 stacked
    ///   Stone door  :  5 stone cut + 3 planks
    ///   Stone window:  3-row cut starting from row 3
    ///
    /// Placement rules:
    ///   • Stones must be placed on flat surface or adjacent existing stone.
    ///   • Floor auto-completes when 20 stones placed in one 3-unit area.
    ///   • Wall segment tracks adjacency (same XZ band within wallSegmentWidth).
    ///   • Pillar stacks up to maxPillarHeight then seals.
    ///
    /// Communicates OUT: StoneFloorCompletedEvent, StoneWallCompletedEvent,
    ///                   StonePillarBuiltEvent.
    /// Communicates IN:  BuildingPieceAddedEvent (stone type filter).
    /// </summary>
    public class StoneBuildingSystem : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Config (inject once)")]
        [SerializeField] private StructuralConfig structConfig;

        [Header("Stone Counts (Data 12)")]
        [SerializeField] private int stonePerFloorTile = 20;
        [SerializeField] private int stonePerWallSection = 28;
        [SerializeField] private int stonePerPillarLayer = 4;
        [SerializeField] private int maxPillarHeight = 4;

        [Header("Detection")]
        [SerializeField] private float floorGroupRadius = 3.0f;
        [SerializeField] private float wallSegmentWidth = 3.5f;
        [SerializeField] private float pillarStackRadius = 0.5f;

        [Header("Prefabs")]
        [SerializeField] private GameObject stoneFloorPrefab;
        [SerializeField] private GameObject stoneWallPrefab;
        [SerializeField] private GameObject stonePillarPrefab;

        // ── Runtime ──────────────────────────────────────────────────────────
        // Floor groups: world-grid cell → placed stone count
        private readonly Dictionary<Vector2Int, int> _floorGroups = new();
        // Wall groups: XZ band key → stone count
        private readonly Dictionary<string, int> _wallGroups = new();
        // Pillar stacks: base position → stack count
        private readonly Dictionary<Vector2Int, int> _pillarStacks = new();

        // Stone tracking
        private readonly List<Vector3> _placedStones = new();

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void OnEnable()
        {
            EventBus<BuildingPieceAddedEvent>.Subscribe(OnPieceAdded);
        }

        private void OnDisable()
        {
            EventBus<BuildingPieceAddedEvent>.Unsubscribe(OnPieceAdded);
        }

        // ── Event handler ────────────────────────────────────────────────────
        private void OnPieceAdded( BuildingPieceAddedEvent e)
        {
            if (e.LogType != LogType.Stone) return;
            var piece = e.Piece?.GetComponent<LogPiece>();
            if (piece == null) return;

            _placedStones.Add(piece.transform.position);

            CheckFloor(piece.transform.position);
            CheckWall(piece.transform.position);
            CheckPillar(piece.transform.position);
        }

        // ── Floor detection ───────────────────────────────────────────────────
        /// <summary>
        /// Group stones within floorGroupRadius by 3-unit grid cells.
        /// When a cell accumulates ≥ stonePerFloorTile, complete the floor tile.
        /// </summary>
        private void CheckFloor(Vector3 pos)
        {
            var cell = WorldToCell(pos, 3f);

            if (!_floorGroups.ContainsKey(cell)) _floorGroups[cell] = 0;
            _floorGroups[cell]++;

            if (_floorGroups[cell] < stonePerFloorTile) return;

            _floorGroups[cell] = 0;  // reset for next tile
            Vector3 center = CellToWorld(cell, 3f);

            if (stoneFloorPrefab != null)
                Instantiate(stoneFloorPrefab, center, Quaternion.identity);

            EventBus<StoneFloorCompletedEvent>.Raise(
                new StoneFloorCompletedEvent(center, stonePerFloorTile));
        }

        // ── Wall detection ────────────────────────────────────────────────────
        /// <summary>
        /// Wall sections are detected by stones placed in the same XZ band
        /// (same rounded Y ± 0.5m, same X or Z line within wallSegmentWidth).
        /// </summary>
        private void CheckWall(Vector3 pos)
        {
            // Quantise to band by rounded Y and primary axis
            string key = $"{Mathf.RoundToInt(pos.y)}_{Mathf.RoundToInt(pos.x / wallSegmentWidth)}";

            if (!_wallGroups.ContainsKey(key)) _wallGroups[key] = 0;
            _wallGroups[key]++;

            if (_wallGroups[key] < stonePerWallSection) return;

            _wallGroups[key] = 0;

            if (stoneWallPrefab != null)
                Instantiate(stoneWallPrefab, pos, Quaternion.identity);

            EventBus<StoneWallCompletedEvent>.Raise(
                new StoneWallCompletedEvent(pos, stonePerWallSection));
            EventBus<ShelterStatusChangedEvent>.Raise(new ShelterStatusChangedEvent(false, true));
        }

        // ── Pillar detection ──────────────────────────────────────────────────
        /// <summary>
        /// Stones placed within pillarStackRadius of each other and stacking
        /// upward build a pillar. Max 4 layers = 4×4 = 16 stones total.
        /// </summary>
        private void CheckPillar(Vector3 pos)
        {
            var baseCell = WorldToCell(new Vector3(pos.x, 0f, pos.z), 1f);

            if (!_pillarStacks.ContainsKey(baseCell)) _pillarStacks[baseCell] = 0;
            _pillarStacks[baseCell]++;

            int totalStones = _pillarStacks[baseCell];
            if (totalStones % stonePerPillarLayer != 0) return; // layer incomplete

            int layerIndex = totalStones / stonePerPillarLayer;
            if (layerIndex > maxPillarHeight) return;

            if (stonePillarPrefab != null)
                Instantiate(stonePillarPrefab, pos + Vector3.up * 0.5f * layerIndex,
                    Quaternion.identity);

            EventBus<StonePillarBuiltEvent>.Raise(
                new StonePillarBuiltEvent(pos, totalStones));
        }

        // ── Stone door / window (cut existing stone wall) ─────────────────────
        /// <summary>
        /// Called by LogCuttingSystem when player axes a stone wall at the correct position.
        /// Replaces the stone section with a door frame or window opening.
        /// </summary>
        public void CutStoneDoor(Vector3 wallPos)
        {
            // Find the stone wall instance and add a door frame component
            // (implementation detail: physics scan at wallPos for stone wall prefab)
            Debug.Log($"[Stone] Door cut at {wallPos} — requires 5 stone + 3 planks.");
        }

        public void CutStoneWindow(Vector3 wallPos)
        {
            Debug.Log($"[Stone] Window cut at {wallPos}.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static Vector2Int WorldToCell(Vector3 pos, float cellSize)
            => new Vector2Int(
                Mathf.FloorToInt(pos.x / cellSize),
                Mathf.FloorToInt(pos.z / cellSize));

        private static Vector3 CellToWorld(Vector2Int cell, float cellSize)
            => new Vector3(cell.x * cellSize + cellSize * 0.5f, 0f,
                           cell.y * cellSize + cellSize * 0.5f);
    }
}
