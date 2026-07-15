
// ═══════════════════════════════════════════════════════════════════════════
// 2.8 — DOOR WINDOW SYSTEM (orchestrator)
// ═══════════════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using TheForest.Building;
using TheForest.Building.Config;
using TheForest.Building.Core;
using TheForest.Building.Data;
using TheForest.Building.Events;
using TheForest.Building.Systems;
using UnityEngine;
using LogType = TheForest.Building.LogType;

/// <summary>
/// 2.8 — Door, Window &amp; Gate System.
///
/// Door creation pipeline:
///   1. Player equips axe, looks at vertical column of wall logs.
///   2. Indicator shows DoorCutMark on the column.
///   3. Two axe swings per log (width cut repeated per row).
///   4. All logs in that column removed → door frame spawned.
///   5. Player places 3 split logs into frame → DoorPiece activates.
///
/// Window: same cut sequence but partial column (leave top + bottom rows).
///
/// Gate:
///   • 5 vertical palisade logs snapped → 6th log placed diagonally
///     → PalisadeGate spawned at position of the 5-log run.
///
/// Lock:
///   • Player holds Stick, looks at FLAT SIDE (inner face) of door.
///   • White arrow indicator appears → place → stick attaches → door locked.
///
/// Communicates OUT: DoorCreatedEvent, DoorToggledEvent, DoorLockedEvent,
///                   GateCreatedEvent, GateToggledEvent.
/// Communicates IN:  BuildingPieceAddedEvent (for gate auto-detect),
///                   LogCutEvent (for door column detection).
/// </summary>
public class DoorWindowSystem : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────
    [Header("Config (inject once)")]
    [SerializeField] private PlacementConfig placementConfig;
    [SerializeField] private MaterialDatabase materialDB;

    [Header("Prefabs")]
    [SerializeField] private GameObject doorFramePrefab;
    [SerializeField] private GameObject doorPanelPrefab;   // DoorPiece component
    [SerializeField] private GameObject windowFramePrefab;
    [SerializeField] private GameObject shutterPrefab;
    [SerializeField] private GameObject gatePrefab;        // PalisadeGate component

    [Header("Door Settings")]
    [Tooltip("Split logs required to complete a door panel")]
    [SerializeField] private int splitLogsPerDoor = 3;
    [Tooltip("Height range for door cut: rows between bottomRow and topRow inclusive")]
    [SerializeField] private int doorHeightInLogs = 4;

    // ── Runtime ──────────────────────────────────────────────────────────
    // Track palisade vertical runs for gate detection: positionKey → list of vertical logs
    private readonly Dictionary<Vector2Int, List<LogPiece>> _palisadeRuns = new();
    // Door frames awaiting fill: frame GO → split logs added so far
    private readonly Dictionary<GameObject, int> _doorFrameFills = new();
    // Window frames awaiting shutter: frame GO → shutters added
    private readonly Dictionary<GameObject, int> _windowFills = new();

    // ── Lifecycle ────────────────────────────────────────────────────────
    private void OnEnable()
    {
        EventBus<BuildingPieceAddedEvent>.Subscribe(OnPieceAdded);
        EventBus<LogCutEvent>.Subscribe(OnLogCut);
    }

    private void OnDisable()
    {
        EventBus<BuildingPieceAddedEvent>.Unsubscribe(OnPieceAdded);
        EventBus<LogCutEvent>.Unsubscribe(OnLogCut);
    }

    // ── Door cut detection ────────────────────────────────────────────────
    private void OnLogCut( LogCutEvent e)
    {
        // When a Width cut creates two pieces that are inside a wall,
        // check if a full vertical column has been cleared → spawn door frame.
        CheckForDoorOpening(e.OriginalLog?.transform.position ?? Vector3.zero);
    }

    /// <summary>
    /// After a cut at worldPos, scan for a clear vertical column in the wall.
    /// A clear column means all logs in a vertical X-stack within doorHeightInLogs
    /// have been removed.
    /// </summary>
    private void CheckForDoorOpening(Vector3 cutPos)
    {
        // Check whether a full column (vertically stacked horizontal logs) is now absent
        // by casting rays upward from cut position and counting hits
        int logCount = 0;
        float logH = placementConfig.wallLogHeight;

        for (int row = 0; row < doorHeightInLogs; row++)
        {
            Vector3 checkPos = cutPos + Vector3.up * (row * logH);
            bool hit = Physics.CheckSphere(checkPos, placementConfig.logRadius * 0.8f,
                ~0, QueryTriggerInteraction.Ignore);
            if (hit) logCount++;
        }

        // Column is clear — spawn door frame
        if (logCount == 0 && doorFramePrefab != null)
        {
            var frame = Instantiate(doorFramePrefab, cutPos, Quaternion.identity);
            _doorFrameFills[frame] = 0;
            EventBus<DoorCreatedEvent>.Raise(new DoorCreatedEvent(frame, cutPos));
        }
    }

    // ── Door panel fill ───────────────────────────────────────────────────
    /// <summary>
    /// Called when player places a split log near a door frame.
    /// Driven externally by FreeformPlacementSystem proximity check.
    /// </summary>
    public bool TryFillDoor(GameObject doorFrame, LogType logType)
    {
        if (!_doorFrameFills.ContainsKey(doorFrame)) return false;
        if (logType != LogType.Split && logType != LogType.SplitQuarter) return false;

        _doorFrameFills[doorFrame]++;
        if (_doorFrameFills[doorFrame] >= splitLogsPerDoor)
        {
            ActivateDoor(doorFrame);
            _doorFrameFills.Remove(doorFrame);
        }
        return true;
    }

    private void ActivateDoor(GameObject frame)
    {
        if (doorPanelPrefab == null) return;
        var door = Instantiate(doorPanelPrefab, frame.transform.position, frame.transform.rotation);
        Destroy(frame);
        EventBus<DoorCreatedEvent>.Raise(new DoorCreatedEvent(door, door.transform.position));
    }

    // ── Lock ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Called by InteractionSystem when player places a stick on a door's inner face.
    /// Checks the DoorPiece and toggles its lock state.
    /// </summary>
    public void TryLockDoor(GameObject doorObject)
    {
        var door = doorObject.GetComponent<DoorPiece>();
        door?.ToggleLock();
    }

    // ── Window ────────────────────────────────────────────────────────────
    /// <summary>
    /// Detects a partial column cut and spawns a window frame.
    /// Minimum 1 log above + 1 log below must remain.
    /// </summary>
    public void CreateWindow(Vector3 position, int cutRows)
    {
        if (windowFramePrefab == null) return;
        var frame = Instantiate(windowFramePrefab, position, Quaternion.identity);
        _windowFills[frame] = 0;
    }

    /// <summary>Place a split-quarter shutter into a window frame.</summary>
    public bool TryAddShutter(GameObject windowFrame)
    {
        if (!_windowFills.ContainsKey(windowFrame)) return false;
        _windowFills[windowFrame]++;
        if (_windowFills[windowFrame] >= 2 && shutterPrefab != null)
        {
            Instantiate(shutterPrefab, windowFrame.transform.position, windowFrame.transform.rotation);
            Destroy(windowFrame);
            _windowFills.Remove(windowFrame);
        }
        return true;
    }

    // ── Palisade gate ─────────────────────────────────────────────────────
    private void OnPieceAdded( BuildingPieceAddedEvent e)
    {
        if (e.Orientation != PlacementMode.Vertical) return;

        var piece = e.Piece?.GetComponent<LogPiece>();
        if (piece == null) return;

        // Group palisade logs by rounded XZ position
        var key = GridKey(piece.transform.position);
        if (!_palisadeRuns.ContainsKey(key)) _palisadeRuns[key] = new List<LogPiece>();

        _palisadeRuns[key].Add(piece);

        // 5 consecutive palisade vertical logs → gate eligible
        if (_palisadeRuns[key].Count == 5)
            CheckForGateEligibility(key);
    }

    private void CheckForGateEligibility(Vector2Int key)
    {
        // Sort run by X or Z to ensure linear sequence
        var run = _palisadeRuns[key];
        run.Sort((a, b) =>
            a.transform.position.x.CompareTo(b.transform.position.x));

        // Verify 5 logs form a straight line (all within 1 spacing unit)
        float spacing = placementConfig.palisadeSpacing;
        bool linear = true;
        for (int i = 1; i < run.Count; i++)
        {
            float dist = Vector3.Distance(run[i].transform.position, run[i - 1].transform.position);
            if (dist > spacing * 1.5f) { linear = false; break; }
        }

        if (!linear) return;

        // Gate spawns at the midpoint of the 5-log run
        Vector3 gatePos = run[2].transform.position;
        SpawnGate(gatePos, run);
    }

    private void SpawnGate(Vector3 pos, List<LogPiece> run)
    {
        if (gatePrefab == null) return;

        // Remove the 5 palisade logs
        foreach (var p in run)
        {
            EventBus<BuildingPieceDismantledEvent>.Raise(
                new BuildingPieceDismantledEvent(p.transform.position, p.LogType, 1));
            Destroy(p.gameObject);
        }

        // Detect gate orientation from run direction
        Vector3 dir = run[run.Count - 1].transform.position - run[0].transform.position;
        var gate = Instantiate(gatePrefab, pos, Quaternion.LookRotation(dir.normalized));
        EventBus<GateCreatedEvent>.Raise(new GateCreatedEvent(gate, pos));
    }

    /// <summary>E key interaction on gate — toggle open/close.</summary>
    public void InteractGate(GameObject gateObject)
        => gateObject.GetComponent<PalisadeGate>()?.ToggleGate();

    // ── Helpers ───────────────────────────────────────────────────────────
    private static Vector2Int GridKey(Vector3 pos)
        => new Vector2Int(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.z));
}
