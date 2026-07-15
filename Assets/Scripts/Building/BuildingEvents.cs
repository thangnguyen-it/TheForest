using UnityEngine;

namespace TheForest.Building.Events
{
    // ═══════════════════════════════════════════════════════════════════════════
    // 2.1  INDICATOR SYSTEM
    // ═══════════════════════════════════════════════════════════════════════════

    public readonly struct IndicatorTypeChangedEvent
    {
        public readonly IndicatorType Type;
        public readonly Vector3 WorldPosition;
        public readonly Vector3 SurfaceNormal;
        public readonly PlacementMode CurrentMode;

        public IndicatorTypeChangedEvent(IndicatorType t, Vector3 pos, Vector3 n, PlacementMode m)
        { Type = t; WorldPosition = pos; SurfaceNormal = n; CurrentMode = m; }
    }

    public readonly struct PlacementValidityChangedEvent
    {
        public readonly bool IsValid;
        public readonly string Reason; // null when valid
        public PlacementValidityChangedEvent(bool valid, string reason = null)
        { IsValid = valid; Reason = reason; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2.2  LOG TYPE & MATERIAL SYSTEM
    // ═══════════════════════════════════════════════════════════════════════════

    public readonly struct LogPickedUpEvent
    {
        public readonly GameObject LogObject;
        public readonly LogType LogType;
        public LogPickedUpEvent(GameObject obj, LogType t) { LogObject = obj; LogType = t; }
    }

    public readonly struct LogDroppedEvent
    {
        public readonly GameObject LogObject;
        public readonly LogType LogType;
        public readonly Vector3 DropPosition;
        public LogDroppedEvent(GameObject obj, LogType t, Vector3 pos)
        { LogObject = obj; LogType = t; DropPosition = pos; }
    }

    public readonly struct LogPlacedEvent
    {
        public readonly GameObject LogObject;
        public readonly LogType LogType;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly int ZoneId;
        public LogPlacedEvent(GameObject obj, LogType t, Vector3 pos, Quaternion rot, int zone)
        { LogObject = obj; LogType = t; Position = pos; Rotation = rot; ZoneId = zone; }
    }

    public readonly struct LogCutEvent
    {
        public readonly GameObject OriginalLog;
        public readonly LogType ResultTypeA;
        public readonly LogType ResultTypeB;
        public readonly CutActionType CutType;
        public LogCutEvent(GameObject orig, LogType a, LogType b, CutActionType ct)
        { OriginalLog = orig; ResultTypeA = a; ResultTypeB = b; CutType = ct; }
    }

    public readonly struct LogSharpenedEvent
    {
        public readonly GameObject LogObject;
        public readonly Vector3 SpikePosition;
        public LogSharpenedEvent(GameObject obj, Vector3 pos) { LogObject = obj; SpikePosition = pos; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2.3  FREEFORM PLACEMENT SYSTEM
    // ═══════════════════════════════════════════════════════════════════════════

    public readonly struct PlacementModeChangedEvent
    {
        public readonly PlacementMode OldMode;
        public readonly PlacementMode NewMode;
        public PlacementModeChangedEvent(PlacementMode old, PlacementMode next)
        { OldMode = old; NewMode = next; }
    }

    public readonly struct SnapPointFoundEvent
    {
        public readonly Vector3 SnapPosition;
        public readonly Quaternion SnapRotation;
        public readonly GameObject SnapTarget;
        public SnapPointFoundEvent(Vector3 pos, Quaternion rot, GameObject target)
        { SnapPosition = pos; SnapRotation = rot; SnapTarget = target; }
    }

    public readonly struct BuildingPieceDroppedEvent
    {
        public readonly GameObject Piece;
        public readonly LogType LogType;
        public readonly Vector3 Position;
        public BuildingPieceDroppedEvent(GameObject p, LogType t, Vector3 pos)
        { Piece = p; LogType = t; Position = pos; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2.4  LOG CUTTING SYSTEM
    // ═══════════════════════════════════════════════════════════════════════════

    public readonly struct CutAttemptedEvent
    {
        public readonly GameObject TargetLog;
        public readonly CutActionType CutType;
        public CutAttemptedEvent(GameObject log, CutActionType ct) { TargetLog = log; CutType = ct; }
    }

    public readonly struct CutCompletedEvent
    {
        public readonly LogType ResultA;
        public readonly LogType ResultB;
        public readonly Vector3 CutPosition;
        public CutCompletedEvent(LogType a, LogType b, Vector3 pos)
        { ResultA = a; ResultB = b; CutPosition = pos; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2.5  BLUEPRINT SYSTEM
    // ═══════════════════════════════════════════════════════════════════════════

    public readonly struct BlueprintSelectedEvent
    {
        public readonly string BlueprintId;
        public readonly bool IsHidden;
        public BlueprintSelectedEvent(string id, bool hidden) { BlueprintId = id; IsHidden = hidden; }
    }

    public readonly struct BlueprintPlacedEvent
    {
        public readonly string BlueprintId;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public BlueprintPlacedEvent(string id, Vector3 pos, Quaternion rot)
        { BlueprintId = id; Position = pos; Rotation = rot; }
    }

    public readonly struct BlueprintMaterialAddedEvent
    {
        public readonly string BlueprintId;
        public readonly string MaterialId;
        public readonly int AmountAdded;
        public readonly int RemainingTotal;
        public BlueprintMaterialAddedEvent(string bp, string mat, int added, int remaining)
        { BlueprintId = bp; MaterialId = mat; AmountAdded = added; RemainingTotal = remaining; }
    }

    public readonly struct BlueprintCompletedEvent
    {
        public readonly string BlueprintId;
        public readonly Vector3 Position;
        public BlueprintCompletedEvent(string id, Vector3 pos) { BlueprintId = id; Position = pos; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2.6  STRUCTURAL INTEGRITY & DEPENDENCY GRAPH
    // ═══════════════════════════════════════════════════════════════════════════

    public readonly struct BuildingPieceAddedEvent
    {
        public readonly GameObject Piece;
        public readonly LogType LogType;
        public readonly PlacementMode Orientation;
        public BuildingPieceAddedEvent(GameObject p, LogType t, PlacementMode o)
        { Piece = p; LogType = t; Orientation = o; }
    }

    public readonly struct BuildingPieceDismantledEvent
    {
        public readonly Vector3 Position;
        public readonly LogType LogType;
        public readonly int MaterialsReturned;
        public BuildingPieceDismantledEvent(Vector3 pos, LogType t, int mats)
        { Position = pos; LogType = t; MaterialsReturned = mats; }
    }

    public readonly struct StrutPlacedEvent
    {
        public readonly Vector3 Position;
        public readonly GameObject CornerPieceA;
        public readonly GameObject CornerPieceB;
        public StrutPlacedEvent(Vector3 pos, GameObject a, GameObject b)
        { Position = pos; CornerPieceA = a; CornerPieceB = b; }
    }

    public readonly struct PieceImmutableEvent
    {
        public readonly GameObject Piece;
        public readonly Vector3 Position;
        public PieceImmutableEvent(GameObject p, Vector3 pos) { Piece = p; Position = pos; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2.7  WALL, ROOF & ADVANCED STRUCTURES
    // ═══════════════════════════════════════════════════════════════════════════

    public readonly struct WallCompletedEvent
    {
        public readonly int LogCount;
        public readonly bool IsPalisade;
        public readonly Vector3 WallCenter;
        public WallCompletedEvent(int count, bool pal, Vector3 center)
        { LogCount = count; IsPalisade = pal; WallCenter = center; }
    }

    public readonly struct RoofCompletedEvent
    {
        public readonly bool IsSlanted;
        public readonly bool BlocksRain;
        public readonly Vector3 RoofCenter;
        public RoofCompletedEvent(bool slanted, bool rain, Vector3 center)
        { IsSlanted = slanted; BlocksRain = rain; RoofCenter = center; }
    }

    public readonly struct SpikeCreatedEvent
    {
        public readonly GameObject SpikePiece;
        public readonly Vector3 Position;
        public SpikeCreatedEvent(GameObject p, Vector3 pos) { SpikePiece = p; Position = pos; }
    }

    public readonly struct RainBlockedEvent
    {
        public readonly bool IsBlocked;
        public readonly int SplitLogCount;
        public RainBlockedEvent(bool blocked, int count) { IsBlocked = blocked; SplitLogCount = count; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2.8  DOOR, WINDOW & GATE SYSTEM
    // ═══════════════════════════════════════════════════════════════════════════

    public readonly struct DoorCreatedEvent
    {
        public readonly GameObject DoorObject;
        public readonly Vector3 Position;
        public DoorCreatedEvent(GameObject obj, Vector3 pos) { DoorObject = obj; Position = pos; }
    }

    public readonly struct DoorToggledEvent
    {
        public readonly GameObject DoorObject;
        public readonly bool IsOpen;
        public DoorToggledEvent(GameObject obj, bool open) { DoorObject = obj; IsOpen = open; }
    }

    public readonly struct DoorLockedEvent
    {
        public readonly GameObject DoorObject;
        public readonly bool IsLocked;
        public DoorLockedEvent(GameObject obj, bool locked) { DoorObject = obj; IsLocked = locked; }
    }

    public readonly struct GateCreatedEvent
    {
        public readonly GameObject GateObject;
        public readonly Vector3 Position;
        public GateCreatedEvent(GameObject obj, Vector3 pos) { GateObject = obj; Position = pos; }
    }

    public readonly struct GateToggledEvent
    {
        public readonly GameObject GateObject;
        public readonly bool IsOpen;
        public GateToggledEvent(GameObject obj, bool open) { GateObject = obj; IsOpen = open; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2.9  ELECTRICITY SYSTEM
    // ═══════════════════════════════════════════════════════════════════════════

    public readonly struct CircuitPoweredEvent
    {
        public readonly GameObject SourceNode;
        public readonly int PoweredConsumerCount;
        public CircuitPoweredEvent(GameObject src, int count)
        { SourceNode = src; PoweredConsumerCount = count; }
    }

    public readonly struct CircuitBrokenEvent
    {
        public readonly GameObject BreakPoint;
        public readonly int LostConsumerCount;
        public CircuitBrokenEvent(GameObject bp, int count)
        { BreakPoint = bp; LostConsumerCount = count; }
    }

    public readonly struct BatteryDepletedEvent
    {
        public readonly GameObject BatteryObject;
        public BatteryDepletedEvent(GameObject obj) { BatteryObject = obj; }
    }

    public readonly struct SolarGeneratingEvent
    {
        public readonly bool IsGenerating;
        public readonly float OutputWatts;
        public SolarGeneratingEvent(bool gen, float watts) { IsGenerating = gen; OutputWatts = watts; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2.10  TARP & ROPE SYSTEM
    // ═══════════════════════════════════════════════════════════════════════════

    public readonly struct TarpDeployedEvent
    {
        public readonly GameObject TarpObject;
        public readonly TarpMode Mode;
        public TarpDeployedEvent(GameObject obj, TarpMode m) { TarpObject = obj; Mode = m; }
    }

    public readonly struct RopeAttachedEvent
    {
        public readonly Vector3 Anchor1;
        public readonly Vector3 Anchor2;
        public readonly bool IsBridge;
        public RopeAttachedEvent(Vector3 a1, Vector3 a2, bool bridge)
        { Anchor1 = a1; Anchor2 = a2; IsBridge = bridge; }
    }

    public readonly struct ZiplineRideEvent
    {
        public readonly Vector3 StartPoint;
        public readonly Vector3 EndPoint;
        public readonly bool Started; // false = ended
        public ZiplineRideEvent(Vector3 s, Vector3 e, bool started)
        { StartPoint = s; EndPoint = e; Started = started; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2.11  LOG STORAGE & TRANSPORT
    // ═══════════════════════════════════════════════════════════════════════════

    public readonly struct LogStoredEvent
    {
        public readonly LogType LogType;
        public readonly int Count;
        public readonly GameObject Holder;
        public LogStoredEvent(LogType t, int c, GameObject h) { LogType = t; Count = c; Holder = h; }
    }

    public readonly struct LogRetrievedEvent
    {
        public readonly LogType LogType;
        public readonly int Count;
        public LogRetrievedEvent(LogType t, int c) { LogType = t; Count = c; }
    }

    public readonly struct SledAttachedEvent
    {
        public readonly GameObject SledObject;
        public readonly int Capacity;
        public SledAttachedEvent(GameObject sled, int cap) { SledObject = sled; Capacity = cap; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2.12  DAMAGE & REPAIR
    // ═══════════════════════════════════════════════════════════════════════════

    public readonly struct BuildingPieceDamagedEvent
    {
        public readonly GameObject Piece;
        public readonly float DamageAmount;
        public readonly DamageState NewState;
        public readonly Vector3 Position;
        public BuildingPieceDamagedEvent(GameObject p, float dmg, DamageState s, Vector3 pos)
        { Piece = p; DamageAmount = dmg; NewState = s; Position = pos; }
    }

    public readonly struct BuildingPieceDestroyedEvent
    {
        public readonly LogType LogType;
        public readonly Vector3 Position;
        public BuildingPieceDestroyedEvent(LogType t, Vector3 pos) { LogType = t; Position = pos; }
    }

    public readonly struct BuildingPieceRepairedEvent
    {
        public readonly GameObject Piece;
        public readonly Vector3 Position;
        public BuildingPieceRepairedEvent(GameObject p, Vector3 pos) { Piece = p; Position = pos; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2.13  STONE BUILDING
    // ═══════════════════════════════════════════════════════════════════════════

    public readonly struct StoneFloorCompletedEvent
    {
        public readonly Vector3 Center;
        public readonly int TileCount;
        public StoneFloorCompletedEvent(Vector3 c, int t) { Center = c; TileCount = t; }
    }

    public readonly struct StoneWallCompletedEvent
    {
        public readonly Vector3 Center;
        public readonly int StoneUsed;
        public StoneWallCompletedEvent(Vector3 c, int s) { Center = c; StoneUsed = s; }
    }

    public readonly struct StonePillarBuiltEvent
    {
        public readonly Vector3 Position;
        public readonly int StoneCount;
        public StonePillarBuiltEvent(Vector3 pos, int c) { Position = pos; StoneCount = c; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2.14  KELVIN BUILDING COMMANDS
    // ═══════════════════════════════════════════════════════════════════════════

    public readonly struct KelvinBuildingCommandIssuedEvent
    {
        public readonly KelvinBuildingTask Task;
        public readonly Vector3 TargetPosition;
        public KelvinBuildingCommandIssuedEvent(KelvinBuildingTask t, Vector3 pos)
        { Task = t; TargetPosition = pos; }
    }

    public readonly struct KelvinBuildingTaskStartedEvent
    {
        public readonly KelvinBuildingTask Task;
        public KelvinBuildingTaskStartedEvent(KelvinBuildingTask t) { Task = t; }
    }

    public readonly struct KelvinBuildingTaskCompletedEvent
    {
        public readonly KelvinBuildingTask Task;
        public readonly bool Success;
        public KelvinBuildingTaskCompletedEvent(KelvinBuildingTask t, bool s) { Task = t; Success = s; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CROSS-SYSTEM EVENTS (Building → Survival / Enemy / Kelvin)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Raised when a fire structure is placed — Survival listens to grant warmth bonus.</summary>
    public readonly struct StructureFirePlacedEvent
    {
        public readonly Vector3 Position;
        public StructureFirePlacedEvent(Vector3 pos) { Position = pos; }
    }

    /// <summary>Raised when roof/wall status changes — Survival listens for shelter temperature bonus.</summary>
    public readonly struct ShelterStatusChangedEvent
    {
        public readonly bool HasRoof;
        public readonly bool HasWalls;
        public ShelterStatusChangedEvent(bool roof, bool walls) { HasRoof = roof; HasWalls = walls; }
    }

    /// <summary>Raised by Enemy system when it attacks a structure — Building listens to apply damage.</summary>
    public readonly struct StructureAttackedEvent
    {
        public readonly GameObject AttackedPiece;
        public readonly float DamageAmount;
        public readonly Vector3 AttackerPosition;
        public StructureAttackedEvent(GameObject piece, float dmg, Vector3 pos)
        { AttackedPiece = piece; DamageAmount = dmg; AttackerPosition = pos; }
    }

    /// <summary>Raised by Survival when player sprints — Building listens to block placement.</summary>
    public readonly struct PlayerSprintStateChangedEvent
    {
        public readonly bool IsSprinting;
        public PlayerSprintStateChangedEvent(bool s) { IsSprinting = s; }
    }
}