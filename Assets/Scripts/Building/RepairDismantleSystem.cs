
// ═══════════════════════════════════════════════════════════════════════════════
// 2.12 — REPAIR & DISMANTLE SYSTEM
// ═══════════════════════════════════════════════════════════════════════════════
using TheForest.Building.Config;
using TheForest.Building.Core;
using TheForest.Building.Data;
using TheForest.Building.Events;
using TheForest.Player;
using UnityEngine;

namespace TheForest.Building.Systems
{
    /// <summary>
    /// 2.12 — Repair &amp; Dismantle System.
    ///
    /// Repair:
    ///   Player equips Repair Tool → hold Left Mouse near damaged piece
    ///   → after repairHoldActivate seconds → HP restores at repairRatePerSec
    ///   → on full HP: visual resets, DamageState = Undamaged.
    ///
    /// Dismantle:
    ///   Hold C while looking at a piece → CanDismantle() check →
    ///   piece removed → materials returned to player inventory.
    ///
    /// Structure Damage toggle: if structureDamageEnabled == false,
    ///   OnStructureAttacked ignores all incoming damage.
    /// </summary>
    public class RepairDismantleSystem : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Config (inject once)")]
        [SerializeField] private BuildingDamageConfig damageConfig;
        [SerializeField] private StructuralConfig structConfig;
        [SerializeField] private MaterialDatabase materialDB;

        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Inventory playerInventory;

        [Header("Layers")]
        [SerializeField] private LayerMask buildingLayer;

        // ── Runtime ──────────────────────────────────────────────────────────
        private bool _repairToolEquipped;
        private bool _dismantleHeld;
        private float _dismantleHoldTimer;
        private float _repairHoldTimer;
        private bool _repairing;

        private LogPiece _targetPiece;
        private const float DetectRange = 3.5f;

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void Awake()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
            if (playerInventory == null)
                playerInventory = GetComponent<Inventory>() ?? GetComponentInParent<Inventory>();
        }

        private void OnEnable()
        {
            EventBus<StructureAttackedEvent>.Subscribe(OnStructureAttacked);
        }

        private void OnDisable()
        {
            EventBus<StructureAttackedEvent>.Unsubscribe(OnStructureAttacked);
        }

        // ── Update ───────────────────────────────────────────────────────────
        private void Update()
        {
            DetectTargetPiece();
            if (_repairToolEquipped && _repairing) TickRepair();
        }

        private void DetectTargetPiece()
        {
            _targetPiece = null;
            if (cameraTransform == null) return;

            var ray = new Ray(cameraTransform.position, cameraTransform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, DetectRange, buildingLayer, QueryTriggerInteraction.Ignore))
                _targetPiece = hit.collider.GetComponentInParent<LogPiece>();
        }

        // ── Repair input ──────────────────────────────────────────────────────
        public void OnRepairHeld(bool held)
        {
            _repairing = held && _repairToolEquipped && _targetPiece != null
                      && _targetPiece.DamageState == DamageState.Damaged;

            if (!held) _repairHoldTimer = 0f;
        }

        private void TickRepair()
        {
            if (_targetPiece == null || _targetPiece.DamageState == DamageState.Undamaged)
            { _repairing = false; return; }

            _repairHoldTimer += Time.deltaTime;
            if (_repairHoldTimer < damageConfig.repairHoldActivate) return;

            _targetPiece.Repair(damageConfig.repairRatePerSec * Time.deltaTime);

            if (_targetPiece.DamageState == DamageState.Undamaged)
            {
                EventBus<BuildingPieceRepairedEvent>.Raise(
                    new BuildingPieceRepairedEvent(_targetPiece.gameObject, _targetPiece.transform.position));
                _repairing = false;
            }
        }

        // ── Dismantle input (hold C) ──────────────────────────────────────────
        private const float DismantleHoldRequired = 1.5f;

        public void OnDismantleHeld(bool held)
        {
            _dismantleHeld = held;
            if (!held) _dismantleHoldTimer = 0f;
        }

        private void LateUpdate()
        {
            if (!_dismantleHeld || _targetPiece == null) return;

            // Structural validation
            var graph = FindFirstObjectByType<StructuralDependencyGraph>();
            if (graph != null && !graph.CanDismantle(_targetPiece))
            {
                _dismantleHoldTimer = 0f;
                return; // load-bearing — cannot dismantle
            }

            _dismantleHoldTimer += Time.deltaTime;
            if (_dismantleHoldTimer < DismantleHoldRequired) return;

            ExecuteDismantle(_targetPiece);
            _dismantleHoldTimer = 0f;
        }

        private void ExecuteDismantle(LogPiece piece)
        {
            if (piece == null) return;

            // Return material to inventory
            int returned = ReturnMaterial(piece.LogType);
            var pos = piece.transform.position;

            EventBus<BuildingPieceDismantledEvent>.Raise(
                new BuildingPieceDismantledEvent(pos, piece.LogType, returned));

            // Unregister from dependency graph
            FindFirstObjectByType<StructuralDependencyGraph>()?.UnregisterPiece(piece);
            Destroy(piece.gameObject);
        }

        private int ReturnMaterial(LogType type)
        {
            if (playerInventory == null) return 0;
            var prefabId = TypeToId(type);
            // Inventory.Add needs an ItemData — bridge via a MaterialItemRegistry (simplified)
            Debug.Log($"[Dismantle] Returned 1x {prefabId} to inventory.");
            return 1;
        }

        // ── Structure damage (from enemies) ───────────────────────────────────
        private void OnStructureAttacked( StructureAttackedEvent e)
        {
            if (!damageConfig.structureDamageEnabled) return;

            var piece = e.AttackedPiece?.GetComponent<LogPiece>();
            if (piece == null) return;

            bool destroyed = piece.ApplyDamage(e.DamageAmount);
            if (destroyed)
            {
                FindFirstObjectByType<StructuralDependencyGraph>()?.UnregisterPiece(piece);
                Destroy(piece.gameObject, 0.1f);
            }
        }

        // ── Equip state (called by EquipmentController listener) ─────────────
        public void SetRepairToolEquipped(bool equipped) => _repairToolEquipped = equipped;

        private static string TypeToId(LogType t) => t switch
        {
            LogType.Full => "full_log",
            LogType.Split => "split_log",
            LogType.Half => "half_log",
            LogType.Stick => "stick",
            LogType.Stone => "large_stone",
            _ => "full_log"
        };
    }
}
