using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TheForest.Interaction;
using TheForest.Items;
using TheForest.Multiplayer;
using TheForest.Player;
using TheForest.Persistence;

namespace TheForest.Building
{
    /// <summary>
    /// Kho chứa DÙNG CHUNG cho TOÀN BỘ nhóm Storage ở Block 6 — mỗi loại là 1 PREFAB riêng, chỉ khác
    /// acceptedItems + capacity + displayName cấu hình qua Inspector, dùng NGUYÊN class này:
    ///
    ///   Stick Storage      -> acceptedItems = [Stick]
    ///   Rock Storage       -> acceptedItems = [Rock]
    ///   Stone Storage      -> acceptedItems = [Large Stone]
    ///   Bone Storage       -> acceptedItems = [Bone]
    ///   Firewood Storage   -> acceptedItems = [Firewood]
    ///   Spear Rack         -> acceptedItems = [Spear]
    ///   Weapon Rack        -> acceptedItems = [tất cả ItemData category Weapon/Tool cầm tay]
    ///   Wall Weapon Rack   -> giống Weapon Rack (chỉ khác mesh/anchor gắn tường, không khác logic)
    ///   Armor Rack         -> acceptedItems = [tất cả armor: Bone/Creepy/Tech/Hide/Leaf], capacity ví dụ 50
    ///   Shelf / Wall Shelf -> acceptedItems = RỖNG (mảng 0 phần tử) = nhận MỌI loại item
    ///
    /// RIÊNG "Log Storage" / "Large Log Storage": KHÔNG dùng class này — log là world object (LogPiece),
    /// dùng LogHolder.cs đã có sẵn (đã fix ở Block 7) với maxCapacity nhỏ hơn (Log Storage) hoặc lớn hơn
    /// (Large Log Storage) tuỳ prefab.
    ///
    /// Nguyên liệu craft từng loại (6-40 Sticks tuỳ loại) xử lý ở tầng BlueprintData khi dựng kho,
    /// không thuộc runtime này.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class StorageContainer : NetworkBehaviour, IInteractable, IPersistentStateParticipant
    {
        [Serializable]
        public class StoredItem
        {
            public ItemData item;
            public int count;
        }

        [Header("Cấu hình")]
        [Tooltip("Loại item được phép cất. Để RỖNG = chấp nhận MỌI loại (dùng cho Shelf/Wall Shelf).")]
        [SerializeField] private ItemData[] acceptedItems;
        [SerializeField] private int capacity = 20;
        [SerializeField] private string displayName = "Kho chứa";

        private readonly List<StoredItem> _stored = new List<StoredItem>();
        private string _persistenceId;
        private ItemData[] _itemCache;
        private NetworkWorldObjectState _networkState;

        public int TotalStoredCount
        {
            get { int t = 0; foreach (var s in _stored) t += s.count; return t; }
        }
        public bool IsFull => TotalStoredCount >= capacity;
        public IReadOnlyList<StoredItem> Contents => _stored;

        public event Action OnStorageChanged;

        [Serializable]
        private sealed class StorageState
        {
            public List<StorageEntryState> items = new List<StorageEntryState>();
        }

        [Serializable]
        private sealed class StorageEntryState
        {
            public string itemId;
            public int count;
        }

        public string PersistenceId => string.IsNullOrEmpty(_persistenceId)
            ? _persistenceId = PersistentStateId.For(this)
            : _persistenceId;

        private void Awake()
        {
            _persistenceId = PersistentStateId.For(this);
            _networkState = GetComponent<NetworkWorldObjectState>();
        }

        private bool Accepts(ItemData item)
        {
            if (item == null) return false;
            if (acceptedItems == null || acceptedItems.Length == 0) return true; // Shelf: nhận mọi loại
            foreach (var a in acceptedItems) if (a == item) return true;
            return false;
        }

        /// <summary>Cất item từ túi vào kho. Trả về số lượng KHÔNG cất được (0 = cất hết).</summary>
        public int Store(Inventory inv, ItemData item, int amount)
        {
            if (NetworkWorldInteraction.ShouldRouteToServer(this))
            {
                int leftover = StoreLocal(inv, item, amount);
                StoreServerRpc(new FixedString64Bytes(NetworkWorldInteraction.ItemId(item)), amount);
                return leftover;
            }

            return StoreLocal(inv, item, amount);
        }

        private int StoreLocal(Inventory inv, ItemData item, int amount)
        {
            if (inv == null || !Accepts(item) || amount <= 0) return amount;

            int space = capacity - TotalStoredCount;
            if (space <= 0) return amount;

            int toMove = Mathf.Min(space, amount, inv.GetCount(item));
            if (toMove <= 0) return amount;
            if (!inv.TryConsume(item, toMove)) return amount;

            var slot = FindSlot(item);
            if (slot == null)
            {
                slot = new StoredItem { item = item, count = 0 };
                _stored.Add(slot);
            }
            slot.count += toMove;

            OnStorageChanged?.Invoke();
            PublishNetworkState();
            return amount - toMove;
        }

        /// <summary>Lấy lại vào túi. Trả false nếu không đủ hàng trong kho hoặc túi đầy giữa chừng.</summary>
        public bool Retrieve(Inventory inv, ItemData item, int amount)
        {
            if (NetworkWorldInteraction.ShouldRouteToServer(this))
            {
                bool localResult = RetrieveLocal(inv, item, amount);
                RetrieveServerRpc(new FixedString64Bytes(NetworkWorldInteraction.ItemId(item)), amount);
                return localResult;
            }

            return RetrieveLocal(inv, item, amount);
        }

        private bool RetrieveLocal(Inventory inv, ItemData item, int amount)
        {
            if (inv == null || amount <= 0) return false;

            var slot = FindSlot(item);
            if (slot == null || slot.count < amount) return false;

            int leftover = inv.Add(item, amount);
            int actuallyAdded = amount - leftover;
            slot.count -= actuallyAdded;
            if (slot.count <= 0) _stored.Remove(slot);

            OnStorageChanged?.Invoke();
            PublishNetworkState();
            return leftover == 0;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void StoreServerRpc(FixedString64Bytes itemId, int amount, RpcParams rpcParams = default)
        {
            if (!NetworkWorldInteraction.TryGetPlayerObject(rpcParams.Receive.SenderClientId, out GameObject playerObject)) return;
            StoreLocal(playerObject.GetComponent<Inventory>(), LoadItemById(itemId.ToString()), amount);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RetrieveServerRpc(FixedString64Bytes itemId, int amount, RpcParams rpcParams = default)
        {
            if (!NetworkWorldInteraction.TryGetPlayerObject(rpcParams.Receive.SenderClientId, out GameObject playerObject)) return;
            RetrieveLocal(playerObject.GetComponent<Inventory>(), LoadItemById(itemId.ToString()), amount);
        }

        private StoredItem FindSlot(ItemData item)
        {
            foreach (var s in _stored)
                if (s.item == item) return s;
            return null;
        }

        // ===================== IInteractable =====================
        public string GetPrompt()
        {
            if (IsFull) return $"{displayName} đầy ({TotalStoredCount}/{capacity})";
            if (TotalStoredCount > 0) return $"[E] {displayName} cất/lấy ({TotalStoredCount}/{capacity})";
            return $"[E] {displayName} ({TotalStoredCount}/{capacity})";
        }
        public bool CanInteract() => true;

        public void Interact(GameObject interactor)
        {
            if (interactor == null) return;

            Inventory inv = interactor.GetComponent<Inventory>();
            EquipmentController equipment = interactor.GetComponent<EquipmentController>();
            ItemData equipped = equipment != null ? equipment.EquippedItem : null;

            if (equipped != null && Accepts(equipped) && !IsFull && inv != null && inv.Has(equipped, 1))
            {
                Store(inv, equipped, 1);
                return;
            }

            StoredItem first = FirstStoredItem();
            if (first != null) Retrieve(inv, first.item, 1);
        }

        public void OnFocus() { }
        public void OnLoseFocus() { }

        public string CapturePersistenceState()
        {
            var state = new StorageState();
            foreach (StoredItem stored in _stored)
            {
                if (stored?.item == null || stored.count <= 0) continue;
                state.items.Add(new StorageEntryState { itemId = stored.item.itemId, count = stored.count });
            }
            return JsonUtility.ToJson(state);
        }

        public void RestorePersistenceState(string json)
        {
            StorageState state = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<StorageState>(json);
            _stored.Clear();
            if (state?.items != null)
            {
                foreach (StorageEntryState saved in state.items)
                {
                    if (saved == null || saved.count <= 0) continue;
                    ItemData item = LoadItemById(saved.itemId);
                    if (item != null) _stored.Add(new StoredItem { item = item, count = saved.count });
                }
            }
            OnStorageChanged?.Invoke();
            PublishNetworkState();
        }

        private ItemData LoadItemById(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (_itemCache == null) _itemCache = Resources.LoadAll<ItemData>("SotFData/Items");
            return Array.Find(_itemCache, candidate => candidate != null && candidate.itemId == itemId);
        }

        private void PublishNetworkState()
        {
            if (IsServer) _networkState?.PublishNow();
        }

        private StoredItem FirstStoredItem()
        {
            foreach (StoredItem stored in _stored)
            {
                if (stored != null && stored.item != null && stored.count > 0) return stored;
            }
            return null;
        }
    }
}
