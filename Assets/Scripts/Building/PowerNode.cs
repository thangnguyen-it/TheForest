using System.Collections.Generic;
using UnityEngine;
using TheForest.Building.Config;
using TheForest.Building.Core;
using TheForest.Building.Events;

namespace TheForest.Building.Systems
{
    // ═══════════════════════════════════════════════════════════════════════════
    // POWER NODE — component on every electric structure element
    // ═══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Attached to Solar Panels, Wires, Batteries, and Consumer devices.
    /// Maintains adjacency list (connected PowerNodes) used by CircuitGraph BFS.
    /// </summary>
    public class PowerNode : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private PowerNodeType nodeType = PowerNodeType.Wire;
        [SerializeField] private float consumptionW = 0f; // only for consumers

        [Header("Runtime (read-only)")]
        [SerializeField] private bool _isPowered;

        public PowerNodeType NodeType => nodeType;
        public float ConsumptionW => consumptionW;
        public bool IsPowered => _isPowered;

        private readonly HashSet<PowerNode> _connections = new();

        public IReadOnlyCollection<PowerNode> Connections => _connections;

        // ── Connection management (called by ElectricitySystem) ───────────────
        public void Connect(PowerNode other)
        {
            if (other == null || other == this) return;
            _connections.Add(other);
            other._connections.Add(this);          // bidirectional
        }

        public void Disconnect(PowerNode other)
        {
            _connections.Remove(other);
            other?._connections.Remove(this);
        }

        /// <summary>Set powered state — called only by CircuitGraph after BFS.</summary>
        public void SetPowered(bool powered) => _isPowered = powered;

        private void OnDestroy()
        {
            // Clean up all connections when removed from scene
            foreach (var n in new HashSet<PowerNode>(_connections))
                n._connections.Remove(this);
            _connections.Clear();
        }
    }
}