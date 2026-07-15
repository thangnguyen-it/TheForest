// ─── LogPiece.cs ──────────────────────────────────────────────────────────────
using System.Collections.Generic;
using UnityEngine;
using TheForest.Building.Core;
using TheForest.Building.Events;

namespace TheForest.Building.Data
{
    /// <summary>
    /// Core component attached to every placed building element.
    /// Tracks identity, damage state, and structural dependency relationships.
    /// Never communicates directly with other systems — raises events only.
    /// </summary>
    public class LogPiece : MonoBehaviour
    {
        // ── Serialised identity ──────────────────────────────────────────────
        [Header("Identity")]
        [SerializeField] private LogType logType = LogType.Full;
        [SerializeField] private PlacementMode orientation = PlacementMode.Horizontal;
        [SerializeField] private bool isSpiked;

        [Header("Structural")]
        [SerializeField] private bool isImmutable;
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;
        [SerializeField] private DamageState damageState = DamageState.Undamaged;

        [Header("Snap Points")]
        [SerializeField] private List<SnapPoint> snapPoints = new();

        // ── Dependency graph (set by StructuralDependencyGraph) ──────────────
        private readonly HashSet<LogPiece> _supportedBy = new();
        private readonly HashSet<LogPiece> _supporting = new();

        // ── Properties ───────────────────────────────────────────────────────
        public LogType LogType => logType;
        public PlacementMode Orientation => orientation;
        public bool IsSpiked => isSpiked;
        public bool IsImmutable => isImmutable;
        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public DamageState DamageState => damageState;
        public IReadOnlyCollection<LogPiece> SupportedBy => _supportedBy;
        public IReadOnlyCollection<LogPiece> Supporting => _supporting;
        public IReadOnlyList<SnapPoint> SnapPoints => snapPoints;

        /// <summary>True when no other piece depends on this one and it is not immutable.</summary>
        public bool CanDismantle => _supporting.Count == 0 && !isImmutable;

        // ── Initialisation ───────────────────────────────────────────────────
        /// <summary>Called by FreeformPlacementSystem immediately after instantiation.</summary>
        public void Initialize(LogType type, PlacementMode orient, float health)
        {
            logType = type;
            orientation = orient;
            maxHealth = health;
            currentHealth = health;
            damageState = DamageState.Undamaged;
        }

        public void RestoreState(LogType type, PlacementMode orient, float savedMaxHealth,
            float savedCurrentHealth, bool savedSpiked, bool savedImmutable)
        {
            logType = type;
            orientation = orient;
            maxHealth = Mathf.Max(1f, savedMaxHealth);
            currentHealth = Mathf.Clamp(savedCurrentHealth, 0f, maxHealth);
            isSpiked = savedSpiked;
            isImmutable = savedImmutable;
            damageState = currentHealth <= 0f ? DamageState.Destroyed
                : currentHealth < maxHealth * 0.5f ? DamageState.Damaged
                : DamageState.Undamaged;
        }

        // ── Dependency helpers (called by StructuralDependencyGraph only) ────
        public void AddSupportedBy(LogPiece p) => _supportedBy.Add(p);
        public void RemoveSupportedBy(LogPiece p) => _supportedBy.Remove(p);
        public void AddSupporting(LogPiece p) => _supporting.Add(p);
        public void RemoveSupporting(LogPiece p) => _supporting.Remove(p);

        // ── Damage ────────────────────────────────────────────────────────────
        /// <summary>Apply damage. Returns true if piece was destroyed.</summary>
        public bool ApplyDamage(float damage)
        {
            if (isImmutable || damageState == DamageState.Destroyed) return false;

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            var prev = damageState;

            if (currentHealth <= 0f)
            {
                damageState = DamageState.Destroyed;
                EventBus<BuildingPieceDestroyedEvent>.Raise(
                    new BuildingPieceDestroyedEvent(logType, transform.position));
                return true;
            }

            damageState = currentHealth < maxHealth * 0.5f ? DamageState.Damaged : DamageState.Undamaged;
            if (damageState != prev)
                EventBus<BuildingPieceDamagedEvent>.Raise(
                    new BuildingPieceDamagedEvent(gameObject, damage, damageState, transform.position));
            return false;
        }

        /// <summary>Restore health (Repair Tool).</summary>
        public void Repair(float amount)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            damageState = currentHealth >= maxHealth ? DamageState.Undamaged
                          : currentHealth >= maxHealth * 0.5f ? DamageState.Undamaged
                          : DamageState.Damaged;
        }

        // ── State transitions ────────────────────────────────────────────────
        public void SetImmutable()
        {
            if (isImmutable) return;
            isImmutable = true;
            EventBus<PieceImmutableEvent>.Raise(new PieceImmutableEvent(gameObject, transform.position));
        }

        public void SetSpiked()
        {
            if (isSpiked) return;
            isSpiked = true;
            EventBus<SpikeCreatedEvent>.Raise(new SpikeCreatedEvent(gameObject, transform.position));
        }
    }
}
