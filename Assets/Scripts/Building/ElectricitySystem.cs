// ═══════════════════════════════════════════════════════════════════════════
// 2.9 — ELECTRICITY SYSTEM (Circuit Graph)
// ═══════════════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using TheForest.Building;
using TheForest.Building.Config;
using TheForest.Building.Core;
using TheForest.Building.Events;
using TheForest.Building.Systems;
using UnityEngine;


/// <summary>
/// 2.9 — Electricity System.
///
/// Maintains a graph of PowerNodes. Uses BFS from each source node (Solar Panel)
/// to determine which consumer nodes are reachable through wire edges.
///
/// Circuit is rebuilt whenever a wire is placed or removed.
/// Night-time power drains from Battery if connected to circuit.
///
/// Architecture:
///   Source  → SolarPanel component generates power
///   Wire    → PowerNode type = Wire connects adjacent nodes
///   Consumer → PowerNode type = Consumer, powered = true when BFS reaches it
///
/// Wire placement rule (from Data 13):
///   Wire can only be placed on a log that has physical connection to other logs.
///   Enforced here via connectionRadius check between wire nodes.
///
/// Communicates OUT: CircuitPoweredEvent, CircuitBrokenEvent,
///                   BatteryDepletedEvent, SolarGeneratingEvent.
/// Communicates IN:  BuildingPieceAddedEvent (wire nodes auto-register),
///                   BuildingPieceDismantledEvent (rebuild on remove).
/// </summary>
public class ElectricitySystem : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────
    [Header("Config (inject once)")]
    [SerializeField] private ElectricityConfig config;

    // ── Runtime ──────────────────────────────────────────────────────────
    private readonly List<PowerNode> _allNodes = new();
    private readonly List<SolarPanel> _sources = new();
    private readonly List<BatteryStorage> _batteries = new();
    private readonly List<PowerNode> _consumers = new();
    private readonly List<PowerNode> _wires = new();

    // BFS reuse
    private readonly Queue<PowerNode> _bfsQueue = new();
    private readonly HashSet<PowerNode> _visited = new();

    private bool _dirtyCircuit = true;  // rebuild next Update

    // ── Lifecycle ────────────────────────────────────────────────────────
    private void Awake()
    {
        if (config == null) Debug.LogError("[Electricity] ElectricityConfig missing.", this);
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

    // ── Update ───────────────────────────────────────────────────────────
    private void Update()
    {
        if (_dirtyCircuit) { RebuildCircuit(); _dirtyCircuit = false; }
        TickNightPower();
    }

    // ── Node registration (public — called externally when device is placed) ──
    public void RegisterNode(PowerNode node)
    {
        if (node == null || _allNodes.Contains(node)) return;
        _allNodes.Add(node);

        switch (node.NodeType)
        {
            case PowerNodeType.Source: break; // sources registered via SolarPanel
            case PowerNodeType.Wire: _wires.Add(node); break;
            case PowerNodeType.Consumer: _consumers.Add(node); break;
        }

        AutoConnect(node);
        _dirtyCircuit = true;
    }

    public void RegisterSource(SolarPanel solar)
    {
        if (solar == null || _sources.Contains(solar)) return;
        _sources.Add(solar);
        _dirtyCircuit = true;
    }

    public void RegisterBattery(BatteryStorage battery)
    {
        if (battery != null && !_batteries.Contains(battery))
            _batteries.Add(battery);
    }

    public void UnregisterNode(PowerNode node)
    {
        if (node == null) return;
        _allNodes.Remove(node);
        _wires.Remove(node);
        _consumers.Remove(node);
        _dirtyCircuit = true;
    }

    // ── Auto-connect new wire/node to adjacent nodes ──────────────────────
    /// <summary>
    /// When a wire node is placed, scan nearby nodes within connectionRadius
    /// and form bidirectional connections automatically.
    /// </summary>
    private void AutoConnect(PowerNode newNode)
    {
        if (config == null) return;
        float radiusSq = config.wireConnectionRadius * config.wireConnectionRadius;

        foreach (var other in _allNodes)
        {
            if (other == newNode) continue;
            float distSq = (other.transform.position - newNode.transform.position).sqrMagnitude;
            if (distSq <= radiusSq)
                newNode.Connect(other);
        }
    }

    // ── Circuit rebuild — BFS from all sources ────────────────────────────
    /// <summary>
    /// BFS from each active SolarPanel → find all reachable consumer nodes.
    /// Consumers not reachable are set powered=false.
    /// </summary>
    private void RebuildCircuit()
    {
        // Reset all nodes
        foreach (var n in _allNodes) n.SetPowered(false);

        int totalPowered = 0;

        foreach (var solar in _sources)
        {
            if (solar == null) continue;
            var sourceNode = solar.GetComponent<PowerNode>();
            if (sourceNode == null) continue;

            float availableW = solar.GetOutputWatts();

            // Night: draw from battery if available
            if (availableW < config.minPowerThreshold)
            {
                foreach (var bat in _batteries)
                {
                    if (bat == null || !bat.HasCharge) continue;
                    availableW = config.batteryDischargeRateW;
                    break;
                }
            }

            if (availableW < config.minPowerThreshold) continue;

            // BFS
            _bfsQueue.Clear();
            _visited.Clear();
            _bfsQueue.Enqueue(sourceNode);
            _visited.Add(sourceNode);
            sourceNode.SetPowered(true);

            while (_bfsQueue.Count > 0)
            {
                var current = _bfsQueue.Dequeue();
                foreach (var neighbor in current.Connections)
                {
                    if (_visited.Contains(neighbor)) continue;
                    _visited.Add(neighbor);

                    // Check power budget for consumers
                    if (neighbor.NodeType == PowerNodeType.Consumer)
                    {
                        if (availableW >= neighbor.ConsumptionW)
                        {
                            availableW -= neighbor.ConsumptionW;
                            neighbor.SetPowered(true);
                            totalPowered++;
                        }
                    }
                    else
                    {
                        neighbor.SetPowered(true);
                    }

                    _bfsQueue.Enqueue(neighbor);
                }
            }

            if (totalPowered > 0)
                EventBus<CircuitPoweredEvent>.Raise(
                    new CircuitPoweredEvent(solar.gameObject, totalPowered));
        }

        // Any consumer still un-powered → raise broken
        foreach (var consumer in _consumers)
        {
            if (consumer == null || consumer.IsPowered) continue;
            EventBus<CircuitBrokenEvent>.Raise(new CircuitBrokenEvent(consumer.gameObject, 1));
        }
    }

    // ── Night power draw from battery ─────────────────────────────────────
    private void TickNightPower()
    {
        // Calculate total consumer load
        float totalLoad = 0f;
        foreach (var c in _consumers)
            if (c != null && c.IsPowered) totalLoad += c.ConsumptionW;

        if (totalLoad <= 0f) return;

        float dtH = Time.deltaTime / 3600f; // convert seconds to hours for Wh
        float needed = totalLoad * dtH;

        foreach (var bat in _batteries)
        {
            if (bat == null || !bat.HasCharge) continue;
            float drawn = bat.Draw(needed);
            needed -= drawn;
            if (needed <= 0f) break;
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────
    private void OnPieceAdded(BuildingPieceAddedEvent e)
    {
        // Wire nodes auto-register when their GO has a PowerNode component
        var node = e.Piece?.GetComponent<PowerNode>();
        if (node != null) RegisterNode(node);
    }

    private void OnPieceDismantled( BuildingPieceDismantledEvent e)
    {
        // Find node at dismantled position
        foreach (var n in new List<PowerNode>(_allNodes))
        {
            if (n == null || (n.transform.position - e.Position).sqrMagnitude > 0.25f) continue;
            UnregisterNode(n);
            EventBus<CircuitBrokenEvent>.Raise(new CircuitBrokenEvent(n.gameObject, 0));
            break;
        }
    }
}

 