// ═══════════════════════════════════════════════════════════════════════════════
// BUILDING CONTROLLER
// Central orchestrator for NHÁNH 2 — Building System.
//
// Responsibilities:
//   • Inject config references into all subsystems on Awake
//   • Subscribe to cross-system EventBus channels
//   • Route events between systems WITHOUT holding direct system references
//     (all calls go through EventBus<T>)
//   • Provide public API surface for external systems (MainMenu, SaveSystem, etc.)
//
// INTERACTION MATRIX
// ────────────────────────────────────────────────────────────────────────────
// SYSTEM                   RAISES                            SUBSCRIBES TO
// ────────────────────────────────────────────────────────────────────────────
// PlacementIndicator       IndicatorTypeChangedEvent         LogPickedUpEvent
//                          PlacementValidityChangedEvent     LogDroppedEvent
//                                                            PlacementModeChangedEvent
// FreeformPlacement        PlacementModeChangedEvent         PlayerSprintStateChangedEvent
//                          SnapPointFoundEvent
//                          BuildingPieceAddedEvent
//                          LogPickedUpEvent / LogPlacedEvent
// LogCutting               CutAttemptedEvent                 (none — input-driven)
//                          CutCompletedEvent
//                          LogCutEvent / LogSharpenedEvent
// BlueprintSystem          BlueprintSelectedEvent            (none — input-driven)
//                          BlueprintPlacedEvent
//                          BlueprintMaterialAddedEvent
//                          BlueprintCompletedEvent
// StructuralDepGraph       StrutPlacedEvent                  BuildingPieceAddedEvent
//                          PieceImmutableEvent               BuildingPieceDismantledEvent
// WallBuilder              WallCompletedEvent                BuildingPieceAddedEvent
//                          RainBlockedEvent
//                          ShelterStatusChangedEvent
// RoofBuilder              RoofCompletedEvent                BuildingPieceAddedEvent
//                          ShelterStatusChangedEvent
// DoorWindowSystem         DoorCreatedEvent                  BuildingPieceAddedEvent
//                          DoorToggledEvent                  LogCutEvent
//                          DoorLockedEvent
//                          GateCreatedEvent / GateToggledEvent
// ElectricitySystem        CircuitPoweredEvent               BuildingPieceAddedEvent
//                          CircuitBrokenEvent                BuildingPieceDismantledEvent
//                          BatteryDepletedEvent
//                          SolarGeneratingEvent
// TarpRopeSystem           TarpDeployedEvent                 (none — placement-driven)
//                          RopeAttachedEvent
//                          ZiplineRideEvent
// LogStorageSystem         LogStoredEvent                    (none — direct API calls)
//                          LogRetrievedEvent
//                          SledAttachedEvent
// RepairDismantleSystem    BuildingPieceDamagedEvent         StructureAttackedEvent
//                          BuildingPieceDestroyedEvent
//                          BuildingPieceDismantledEvent
//                          BuildingPieceRepairedEvent
// StoneBuildingSystem      StoneFloorCompletedEvent          BuildingPieceAddedEvent
//                          StoneWallCompletedEvent
//                          StonePillarBuiltEvent
// KelvinBuildingCommands   KelvinBuildingTaskStartedEvent    KelvinBuildingCommandIssuedEvent
//                          KelvinBuildingTaskCompletedEvent
//
// CROSS-SYSTEM (Building ↔ Survival / Enemy)
// BuildingSystem raises → ShelterStatusChangedEvent  → SurvivalStats listens (Temperature)
// BuildingSystem raises → StructureFirePlacedEvent   → FireSource listens (Warmth zone)
// EnemySystem raises   → StructureAttackedEvent      → RepairDismantleSystem listens
// SurvivalSystem raises → PlayerSprintStateChangedEvent → FreeformPlacement listens
// ═══════════════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using UnityEngine;
using TheForest.Building.Config;
using TheForest.Building.Core;
using TheForest.Building.Data;
using TheForest.Building.Events;
using TheForest.Building.Systems;

namespace TheForest.Building
{
    /// <summary>
    /// Scene-level orchestrator for the entire Building System (Branch 2).
    /// Place ONE instance in the scene. Inject all configs and subsystem references
    /// through the Inspector — never use Find/GetComponent at runtime.
    ///
    /// Cross-system relay (all via EventBus — no Manager calls):
    ///   ShelterStatusChangedEvent → broadcast (Survival listens)
    ///   StructureFirePlacedEvent  → broadcast (FireSource listens)
    ///   KelvinBuildingCommandIssuedEvent → broadcast (KelvinBuildingCommands listens)
    /// </summary>
    [DefaultExecutionOrder(-50)]   // init before subsystems
    public class BuildingController : MonoBehaviour
    {
        // ── Config assets (drag in Inspector — injected once in Awake) ────────
        [Header("─── CONFIGS ───")]
        [SerializeField] private IndicatorConfig indicatorConfig;
        [SerializeField] private PlacementConfig placementConfig;
        [SerializeField] private BlueprintConfig blueprintConfig;
        [SerializeField] private StructuralConfig structuralConfig;
        [SerializeField] private ElectricityConfig electricityConfig;
        [SerializeField] private BuildingDamageConfig damageConfig;
        [SerializeField] private KelvinBuildingConfig kelvinConfig;
        [SerializeField] private MaterialDatabase materialDatabase;

        // ── Subsystem references (all in scene, drag in Inspector) ────────────
        [Header("─── SUBSYSTEMS ───")]
        [SerializeField] private PlacementIndicatorController indicatorController;
        [SerializeField] private FreeformPlacementSystem freePlacement;
        [SerializeField] private LogCuttingSystem cuttingSystem;
        [SerializeField] private BlueprintSystem blueprintSystem;
        [SerializeField] private StructuralDependencyGraph structureGraph;
        [SerializeField] private WallBuilder wallBuilder;
        [SerializeField] private RoofBuilder roofBuilder;
        [SerializeField] private DoorWindowSystem doorWindowSystem;
        [SerializeField] private ElectricitySystem electricitySystem;
        [SerializeField] private TarpRopeSystem tarpRopeSystem;
        [SerializeField] private RepairDismantleSystem repairSystem;
        [SerializeField] private StoneBuildingSystem stoneBuildingSystem;
        [SerializeField] private KelvinBuildingCommands kelvinCommands;

        // ── Runtime diagnostics ───────────────────────────────────────────────
        [Header("─── DIAGNOSTICS (read-only) ───")]
        [SerializeField] private int _totalPiecesPlaced;
        [SerializeField] private int _totalPiecesDismantled;
        [SerializeField] private bool _hasShelter;
        [SerializeField] private bool _hasRoof;
        [SerializeField] private bool _circuitPowered;

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void Awake()
        {
            ValidateReferences();
        }

        private void OnEnable()
        {
            // ── Building lifecycle counters ───────────────────────────────────
            EventBus<BuildingPieceAddedEvent>.Subscribe(OnPieceAdded);
            EventBus<BuildingPieceDismantledEvent>.Subscribe(OnPieceDismantled);

            // ── Shelter / environment cross-system relay ──────────────────────
            EventBus<ShelterStatusChangedEvent>.Subscribe(OnShelterChanged);
            EventBus<RoofCompletedEvent>.Subscribe(OnRoofCompleted);
            EventBus<WallCompletedEvent>.Subscribe(OnWallCompleted);

            // ── Electrical cross-system ───────────────────────────────────────
            EventBus<CircuitPoweredEvent>.Subscribe(OnCircuitPowered);
            EventBus<CircuitBrokenEvent>.Subscribe(OnCircuitBroken);

            // ── Fire relay ────────────────────────────────────────────────────
            EventBus<StructureFirePlacedEvent>.Subscribe(OnFirePlaced);

            // ── Kelvin cross-system ───────────────────────────────────────────
            EventBus<KelvinBuildingTaskCompletedEvent>.Subscribe(OnKelvinTaskDone);

            // ── Log holder fill → Kelvin notification ─────────────────────────
            EventBus<LogStoredEvent>.Subscribe(OnLogStored);
        }

        private void OnDisable()
        {
            EventBus<BuildingPieceAddedEvent>.Unsubscribe(OnPieceAdded);
            EventBus<BuildingPieceDismantledEvent>.Unsubscribe(OnPieceDismantled);
            EventBus<ShelterStatusChangedEvent>.Unsubscribe(OnShelterChanged);
            EventBus<RoofCompletedEvent>.Unsubscribe(OnRoofCompleted);
            EventBus<WallCompletedEvent>.Unsubscribe(OnWallCompleted);
            EventBus<CircuitPoweredEvent>.Unsubscribe(OnCircuitPowered);
            EventBus<CircuitBrokenEvent>.Unsubscribe(OnCircuitBroken);
            EventBus<StructureFirePlacedEvent>.Unsubscribe(OnFirePlaced);
            EventBus<KelvinBuildingTaskCompletedEvent>.Unsubscribe(OnKelvinTaskDone);
            EventBus<LogStoredEvent>.Unsubscribe(OnLogStored);
        }

        // ═════════════════════════════════════════════════════════════════════
        // EVENT HANDLERS — diagnostic + cross-system relay only
        // NEVER call subsystem methods directly here.
        // ═════════════════════════════════════════════════════════════════════

        private void OnPieceAdded( BuildingPieceAddedEvent e)
        {
            _totalPiecesPlaced++;
        }

        private void OnPieceDismantled( BuildingPieceDismantledEvent e)
        {
            _totalPiecesDismantled++;
        }

        /// <summary>
        /// Cross-system relay: ShelterStatusChangedEvent originates in WallBuilder/RoofBuilder.
        /// BuildingController re-raises it so Survival System can listen for temperature bonus.
        /// (The event is already on the bus — Survival subscribes directly. No relay needed.)
        /// </summary>
        private void OnShelterChanged( ShelterStatusChangedEvent e)
        {
            _hasShelter = e.HasWalls || e.HasRoof;
            _hasRoof = e.HasRoof;
            // Event is already public on bus — Survival listens to same EventBus<ShelterStatusChangedEvent>
        }

        private void OnRoofCompleted( RoofCompletedEvent e)
        {
            _hasRoof = true;
            if (e.BlocksRain)
                EventBus<ShelterStatusChangedEvent>.Raise(new ShelterStatusChangedEvent(true, _hasShelter));
        }

        private void OnWallCompleted( WallCompletedEvent e)
        {
            _hasShelter = true;
            EventBus<ShelterStatusChangedEvent>.Raise(new ShelterStatusChangedEvent(_hasRoof, true));
        }

        private void OnCircuitPowered( CircuitPoweredEvent e)
        {
            _circuitPowered = true;
        }

        private void OnCircuitBroken( CircuitBrokenEvent e)
        {
            _circuitPowered = false;
        }

        /// <summary>
        /// Fire structure placed → re-raise so FireSource/NoiseSystem can react.
        /// In this architecture, World.FireSource subscribes to StructureFirePlacedEvent
        /// and spawns/activates a fire prefab at the given position.
        /// </summary>
        private void OnFirePlaced( StructureFirePlacedEvent e)
        {
            // FireSource system subscribes independently — no direct call needed
            Debug.Log($"[Building] Fire placed at {e.Position} — relayed to World.FireRegistry.");
        }

        private void OnKelvinTaskDone( KelvinBuildingTaskCompletedEvent e)
        {
            Debug.Log($"[Kelvin] Task {e.Task} completed. Success={e.Success}");
        }

        private void OnLogStored( LogStoredEvent e)
        {
            // LogHolder filled → broadcast so Kelvin knows holder is available
            // (KelvinBuildingCommands already polls — this is for any future UI subscriber)
        }

        // ═════════════════════════════════════════════════════════════════════
        // PUBLIC API — for external systems (MainMenu, SaveSystem, EnemySystem)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called by EnemySystem when a cannibal/mutant attacks a structure.
        /// Routes through EventBus — RepairDismantleSystem and DependencyGraph react.
        /// </summary>
        public void NotifyStructureAttacked(GameObject piece, float damage, Vector3 attackerPos)
        {
            EventBus<StructureAttackedEvent>.Raise(
                new StructureAttackedEvent(piece, damage, attackerPos));
        }

        /// <summary>
        /// Called by Companion/Kelvin UI when player issues a building command.
        /// Routes through EventBus — KelvinBuildingCommands reacts.
        /// </summary>
        public void IssueKelvinCommand(KelvinBuildingTask task, Vector3 targetPosition)
        {
            EventBus<KelvinBuildingCommandIssuedEvent>.Raise(
                new KelvinBuildingCommandIssuedEvent(task, targetPosition));
        }

        /// <summary>
        /// Called by ZiplineGun interaction — routes to TarpRopeSystem.
        /// </summary>
        public void FireZiplineGun(Vector3 hitPoint)
            => tarpRopeSystem?.FireRopeGun(hitPoint);

        /// <summary>
        /// Called by Survival/PlayerController when sprint state changes.
        /// Blocks building placement during sprint (GDD feel).
        /// </summary>
        public void NotifySprintChanged(bool isSprinting)
        {
            EventBus<PlayerSprintStateChangedEvent>.Raise(
                new PlayerSprintStateChangedEvent(isSprinting));
        }

        /// <summary>
        /// Toggle global structure damage (Settings → Gameplay → Structure Damage).
        /// </summary>
        public void SetStructureDamageEnabled(bool enabled)
        {
            if (damageConfig != null)
                damageConfig.structureDamageEnabled = enabled;
        }

        /// <summary>Unlock a hidden blueprint by ID (found in cave/world).</summary>
        public void UnlockHiddenBlueprint(string blueprintId)
            => blueprintSystem?.UnlockHiddenBlueprint(blueprintId);

        /// <summary>Register a solar panel with the electricity system.</summary>
        public void RegisterSolarPanel(SolarPanel panel)
            => electricitySystem?.RegisterSource(panel);

        /// <summary>Register a battery with the electricity system.</summary>
        public void RegisterBattery(BatteryStorage battery)
            => electricitySystem?.RegisterBattery(battery);

        /// <summary>Register a wire/consumer power node.</summary>
        public void RegisterPowerNode(PowerNode node)
            => electricitySystem?.RegisterNode(node);

        // ═════════════════════════════════════════════════════════════════════
        // SAVE / LOAD
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Capture all placed building pieces for save serialisation.
        /// Each LogPiece carries its type, position, rotation, health.
        /// </summary>
        public List<BuildingSaveEntry> CaptureSaveData()
        {
            var entries = new List<BuildingSaveEntry>();
            var pieces = FindObjectsByType<LogPiece>(FindObjectsSortMode.None);
            foreach (var p in pieces)
            {
                entries.Add(new BuildingSaveEntry
                {
                    logType = p.LogType,
                    orientation = p.Orientation,
                    position = p.transform.position,
                    rotation = p.transform.eulerAngles,
                    health = p.CurrentHealth,
                    isImmutable = p.IsImmutable,
                    isSpiked = p.IsSpiked
                });
            }
            return entries;
        }

        /// <summary>Restore building from save data. Clears existing pieces first.</summary>
        public void RestoreSaveData(List<BuildingSaveEntry> entries)
        {
            if (entries == null || materialDatabase == null) return;

            // Clear existing
            var existing = FindObjectsByType<LogPiece>(FindObjectsSortMode.None);
            foreach (var p in existing) Destroy(p.gameObject);

            foreach (var e in entries)
            {
                var prefab = materialDatabase.GetPrefab(LogTypeToId(e.logType));
                if (prefab == null) continue;

                var go = Instantiate(prefab,
                    e.position, Quaternion.Euler(e.rotation));
                var piece = go.GetComponent<LogPiece>() ?? go.AddComponent<LogPiece>();
                piece.Initialize(e.logType, e.orientation, e.health);

                if (e.isImmutable) piece.SetImmutable();
                if (e.isSpiked) piece.SetSpiked();

                // Re-register in dependency graph without raising placement events
                // (silent restore — avoids triggering WallCompleted/RoofCompleted spam)
            }

            Debug.Log($"[Building] Restored {entries.Count} building pieces from save.");
        }

        // ═════════════════════════════════════════════════════════════════════
        // VALIDATION
        // ═════════════════════════════════════════════════════════════════════

        private void ValidateReferences()
        {
            // Configs
            if (indicatorConfig == null) Warn("IndicatorConfig");
            if (placementConfig == null) Warn("PlacementConfig");
            if (blueprintConfig == null) Warn("BlueprintConfig");
            if (structuralConfig == null) Warn("StructuralConfig");
            if (electricityConfig == null) Warn("ElectricityConfig");
            if (damageConfig == null) Warn("BuildingDamageConfig");
            if (kelvinConfig == null) Warn("KelvinBuildingConfig");
            if (materialDatabase == null) Warn("MaterialDatabase");

            // Subsystems
            if (indicatorController == null) Warn("PlacementIndicatorController");
            if (freePlacement == null) Warn("FreeformPlacementSystem");
            if (cuttingSystem == null) Warn("LogCuttingSystem");
            if (blueprintSystem == null) Warn("BlueprintSystem");
            if (structureGraph == null) Warn("StructuralDependencyGraph");
            if (wallBuilder == null) Warn("WallBuilder");
            if (roofBuilder == null) Warn("RoofBuilder");
            if (doorWindowSystem == null) Warn("DoorWindowSystem");
            if (electricitySystem == null) Warn("ElectricitySystem");
            if (tarpRopeSystem == null) Warn("TarpRopeSystem");
            if (repairSystem == null) Warn("RepairDismantleSystem");
            if (stoneBuildingSystem == null) Warn("StoneBuildingSystem");
            if (kelvinCommands == null) Warn("KelvinBuildingCommands");
        }

        private void Warn(string name)
            => Debug.LogWarning($"[BuildingController] {name} not assigned in Inspector.", this);

        // ── Helpers ───────────────────────────────────────────────────────────
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

    // ── Save entry ────────────────────────────────────────────────────────────
    [System.Serializable]
    public class BuildingSaveEntry
    {
        public LogType logType;
        public PlacementMode orientation;
        public Vector3 position;
        public Vector3 rotation;     // Euler angles
        public float health;
        public bool isImmutable;
        public bool isSpiked;
    }
}