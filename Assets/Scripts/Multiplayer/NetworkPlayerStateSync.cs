using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TheForest.Items;
using TheForest.Persistence;
using TheForest.Player;

namespace TheForest.Multiplayer
{
    public struct PlayerVitalsNetworkState : INetworkSerializable, IEquatable<PlayerVitalsNetworkState>
    {
        public float hunger;
        public float thirst;
        public float energy;
        public float stamina;
        public float health;
        public float temperature;
        public bool isWet;
        public bool hasDiedBefore;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref hunger);
            serializer.SerializeValue(ref thirst);
            serializer.SerializeValue(ref energy);
            serializer.SerializeValue(ref stamina);
            serializer.SerializeValue(ref health);
            serializer.SerializeValue(ref temperature);
            serializer.SerializeValue(ref isWet);
            serializer.SerializeValue(ref hasDiedBefore);
        }

        public bool Equals(PlayerVitalsNetworkState other)
        {
            return Mathf.Approximately(hunger, other.hunger) && Mathf.Approximately(thirst, other.thirst) &&
                   Mathf.Approximately(energy, other.energy) && Mathf.Approximately(stamina, other.stamina) &&
                   Mathf.Approximately(health, other.health) && Mathf.Approximately(temperature, other.temperature) &&
                   isWet == other.isWet && hasDiedBefore == other.hasDiedBefore;
        }
    }

    /// <summary>
    /// Associates a stable profile with each network player and keeps the server copy current for saves.
    /// Owner submissions are range and inventory validated before entering authoritative server state.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerStateSync : NetworkBehaviour
    {
        [SerializeField, Min(0.1f)] private float submitInterval = 0.25f;
        [SerializeField, Min(0.1f)] private float publishInterval = 0.25f;

        private readonly NetworkVariable<FixedString64Bytes> _playerId = new NetworkVariable<FixedString64Bytes>();
        private readonly NetworkVariable<PlayerVitalsNetworkState> _vitals = new NetworkVariable<PlayerVitalsNetworkState>();
        private readonly NetworkVariable<FixedString4096Bytes> _inventory = new NetworkVariable<FixedString4096Bytes>();

        private SurvivalStats _stats;
        private Inventory _inventoryComponent;
        private ItemData[] _itemCatalog;
        private float _submitTimer;
        private float _publishTimer;

        [Serializable]
        private sealed class InventoryNetworkState
        {
            public List<InventoryNetworkEntry> items = new List<InventoryNetworkEntry>();
        }

        [Serializable]
        private sealed class InventoryNetworkEntry
        {
            public string itemId;
            public int amount;
        }

        public string PlayerId => _playerId.Value.ToString();

        private void Awake()
        {
            _stats = GetComponent<SurvivalStats>();
            _inventoryComponent = GetComponent<Inventory>();
        }

        public override void OnNetworkSpawn()
        {
            _vitals.OnValueChanged += HandleVitalsChanged;
            _inventory.OnValueChanged += HandleInventoryChanged;

            if (IsServer)
            {
                string id = CoopSessionManager.Instance != null &&
                            CoopSessionManager.Instance.TryGetPlayerId(OwnerClientId, out string resolved)
                    ? resolved
                    : $"client-{OwnerClientId}";
                _playerId.Value = new FixedString64Bytes(id);
                SaveGameManager.Instance?.TryRestorePlayerProfile(id, _stats);
                PublishServerState();
            }
            else if (!IsOwner && _stats != null)
            {
                _stats.enabled = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            _vitals.OnValueChanged -= HandleVitalsChanged;
            _inventory.OnValueChanged -= HandleInventoryChanged;
            if (_stats != null) _stats.enabled = true;
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner && !IsServer)
            {
                _submitTimer += Time.unscaledDeltaTime;
                if (_submitTimer >= submitInterval)
                {
                    _submitTimer = 0f;
                    SubmitOwnerStateRpc(CaptureVitals(), new FixedString4096Bytes(CaptureInventoryJson()));
                }
            }

            if (IsServer)
            {
                _publishTimer += Time.unscaledDeltaTime;
                if (_publishTimer >= publishInterval)
                {
                    _publishTimer = 0f;
                    PublishServerState();
                }
            }
        }

        [Rpc(SendTo.Server)]
        private void SubmitOwnerStateRpc(PlayerVitalsNetworkState submittedVitals,
            FixedString4096Bytes submittedInventory)
        {
            if (!IsValid(submittedVitals)) return;
            ApplyVitals(submittedVitals);
            ApplyInventoryJson(submittedInventory.ToString());
            PublishServerState();
        }

        private void PublishServerState()
        {
            if (!IsServer) return;
            _vitals.Value = CaptureVitals();
            _inventory.Value = new FixedString4096Bytes(CaptureInventoryJson());
        }

        private PlayerVitalsNetworkState CaptureVitals()
        {
            if (_stats == null) return default;
            return new PlayerVitalsNetworkState
            {
                hunger = _stats.HungerCurrent,
                thirst = _stats.ThirstCurrent,
                energy = _stats.EnergyCurrent,
                stamina = _stats.StaminaCurrent,
                health = _stats.HealthCurrent,
                temperature = _stats.Temperature,
                isWet = _stats.IsWet,
                hasDiedBefore = _stats.HasDiedBefore
            };
        }

        private void ApplyVitals(PlayerVitalsNetworkState state)
        {
            _stats?.RestoreState(state.hunger, state.thirst, state.energy, state.stamina,
                state.health, state.temperature, state.isWet, state.hasDiedBefore);
        }

        private void HandleVitalsChanged(PlayerVitalsNetworkState previous, PlayerVitalsNetworkState current)
        {
            if (!IsServer && !IsOwner) ApplyVitals(current);
        }

        private void HandleInventoryChanged(FixedString4096Bytes previous, FixedString4096Bytes current)
        {
            if (!IsServer) ApplyInventoryJson(current.ToString());
        }

        private string CaptureInventoryJson()
        {
            var state = new InventoryNetworkState();
            if (_inventoryComponent != null)
            {
                foreach (InventorySlot slot in _inventoryComponent.Slots)
                {
                    if (slot == null || slot.IsEmpty || string.IsNullOrEmpty(slot.item.itemId)) continue;
                    state.items.Add(new InventoryNetworkEntry { itemId = slot.item.itemId, amount = slot.count });
                }
            }
            return JsonUtility.ToJson(state);
        }

        private void ApplyInventoryJson(string json)
        {
            if (_inventoryComponent == null || string.IsNullOrEmpty(json)) return;
            InventoryNetworkState state;
            try { state = JsonUtility.FromJson<InventoryNetworkState>(json); }
            catch (ArgumentException) { return; }
            if (state?.items == null || state.items.Count > 64) return;

            if (_itemCatalog == null) _itemCatalog = Resources.LoadAll<ItemData>("SotFData/Items");
            var validated = new List<(ItemData item, int amount)>();
            foreach (InventoryNetworkEntry entry in state.items)
            {
                if (entry == null || entry.amount <= 0) continue;
                ItemData item = Array.Find(_itemCatalog,
                    candidate => candidate != null && candidate.itemId == entry.itemId);
                if (item == null || entry.amount > item.maxStack) return;
                validated.Add((item, entry.amount));
            }

            _inventoryComponent.Clear();
            foreach (var entry in validated) _inventoryComponent.Add(entry.item, entry.amount);
        }

        private static bool IsValid(PlayerVitalsNetworkState state)
        {
            return InRange(state.hunger) && InRange(state.thirst) && InRange(state.energy) &&
                   InRange(state.stamina) && InRange(state.health) &&
                   !float.IsNaN(state.temperature) && state.temperature >= -100f && state.temperature <= 100f;
        }

        private static bool InRange(float value)
        {
            return !float.IsNaN(value) && value >= 0f && value <= 100f;
        }
    }
}
