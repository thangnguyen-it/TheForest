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
    /// Giá phơi khô thịt/cá theo GDD (Block 4): 6 slot, mỗi slot cần đúng 500 giây thực (8 phút 20 giây)
    /// để khô hoàn toàn thành item non-perishable. Lấy ra GIỮA CHỪNG (chưa đủ giờ) -> mất tiến trình,
    /// trả về item TƯƠI ban đầu, timer reset về 0 (đúng GDD).
    ///
    /// TODO tích hợp tương lai: dự án hiện CHƯA có hệ thống food-spoilage/perishable riêng (ItemData
    /// không có field kiểu isPerishable/spoilTime) — dryMappings.driedItem nên trỏ tới 1 ItemData được
    /// đánh dấu "không hư" ở TẦNG DATA khi hệ thống spoilage đó được xây (không thuộc phạm vi Block 4).
    ///
    /// Loại thực phẩm sấy được theo GDD: raw meat, cooked meat, raw fish, limbs, heads (KHÔNG sấy được
    /// small meat) — việc này được đảm bảo tự nhiên bằng cách chỉ những ItemData có mặt trong dryMappings
    /// mới được TryPlace() chấp nhận.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class DryingRack : NetworkBehaviour, IInteractable, IPersistentStateParticipant
    {
        /// <summary>8 phút 20 giây = 500 giây thực, theo GDD.</summary>
        public const float DryDurationSeconds = 500f;

        [Header("Slots")]
        [SerializeField] private int slotCount = 6;
        [Tooltip("Điểm gắn model thịt/cá lên giá (tuỳ chọn, để hiển thị trực quan theo từng slot).")]
        [SerializeField] private Transform[] slotVisualAnchors;

        [Header("Ánh xạ item tươi -> item đã sấy khô")]
        [SerializeField] private DryResultMapping[] dryMappings;
        [SerializeField] private string itemResourcePath = "SotFData/Items";

        [Serializable]
        public class DryResultMapping
        {
            public ItemData freshItem;
            public ItemData driedItem;
        }

        private class Slot
        {
            public ItemData item;
            public float timer;
            public bool IsEmpty => item == null;
            public bool IsDone => item != null && timer >= DryDurationSeconds;
        }

        private Slot[] _slots;
        private ItemData[] _itemCache;
        private NetworkWorldObjectState _networkState;

        public event Action OnRackChanged;
        private string _persistenceId;
        public string PersistenceId => string.IsNullOrEmpty(_persistenceId)
            ? _persistenceId = PersistentStateId.For(this)
            : _persistenceId;

        [Serializable]
        private sealed class RackPersistenceState
        {
            public List<RackSlotState> slots = new List<RackSlotState>();
        }

        [Serializable]
        private sealed class RackSlotState
        {
            public string itemId;
            public float timer;
        }

        private void Awake()
        {
            _persistenceId = PersistentStateId.For(this);
            _networkState = GetComponent<NetworkWorldObjectState>();
            _slots = new Slot[Mathf.Max(1, slotCount)];
            for (int i = 0; i < _slots.Length; i++) _slots[i] = new Slot();
        }

        private void Update()
        {
            if (!NetworkWorldInteraction.ShouldSimulateHere(this)) return;
            bool anyJustFinished = false;
            foreach (var s in _slots)
            {
                if (s.IsEmpty || s.timer >= DryDurationSeconds) continue;
                s.timer += Time.deltaTime;
                if (s.timer >= DryDurationSeconds) anyJustFinished = true;
            }
            if (anyJustFinished)
            {
                OnRackChanged?.Invoke();
                PublishNetworkState();
            }
        }

        /// <summary>Đặt 1 miếng thịt/cá (phải có trong dryMappings) vào slot trống đầu tiên.</summary>
        public bool TryPlace(ItemData item)
        {
            if (NetworkWorldInteraction.ShouldRouteToServer(this))
            {
                TryPlaceLocal(item);
                PlaceServerRpc(new FixedString64Bytes(NetworkWorldInteraction.ItemId(item)));
                return true;
            }

            return TryPlaceLocal(item);
        }

        private bool TryPlaceLocal(ItemData item)
        {
            if (item == null || !HasMapping(item)) return false;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    _slots[i].item = item;
                    _slots[i].timer = 0f;
                    ApplySlotVisual(i, item);
                    OnRackChanged?.Invoke();
                    PublishNetworkState();
                    return true;
                }
            }
            return false; // đủ slot
        }

        /// <summary>Lấy 1 slot ra. Chưa đủ giờ -> trả lại item TƯƠI (mất tiến trình theo GDD).</summary>
        public ItemData TakeSlot(int index)
        {
            if (index < 0 || index >= _slots.Length) return null;
            var s = _slots[index];
            if (s.IsEmpty) return null;

            ItemData result = s.IsDone ? GetDriedResult(s.item) : s.item;

            s.item = null;
            s.timer = 0f;
            ApplySlotVisual(index, null);
            OnRackChanged?.Invoke();
            PublishNetworkState();
            return result;
        }

        /// <summary>E nhanh: lấy thẳng vào túi item khô ĐẦU TIÊN sẵn sàng (không đụng slot chưa xong).</summary>
        public bool TryHarvestFirstReady(Inventory inv)
        {
            if (NetworkWorldInteraction.ShouldRouteToServer(this))
            {
                TryHarvestFirstReadyLocal(inv);
                HarvestFirstReadyServerRpc();
                return true;
            }

            return TryHarvestFirstReadyLocal(inv);
        }

        private bool TryHarvestFirstReadyLocal(Inventory inv)
        {
            if (inv == null) return false;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsDone)
                {
                    var result = TakeSlot(i);
                    if (result == null) return false;
                    inv.Add(result, 1);
                    return true;
                }
            }
            return false;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void HarvestFirstReadyServerRpc(RpcParams rpcParams = default)
        {
            if (!NetworkWorldInteraction.TryGetPlayerObject(rpcParams.Receive.SenderClientId, out GameObject playerObject)) return;
            TryHarvestFirstReadyLocal(playerObject.GetComponent<Inventory>());
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void PlaceServerRpc(FixedString64Bytes itemId, RpcParams rpcParams = default)
        {
            if (!NetworkWorldInteraction.TryGetPlayerObject(rpcParams.Receive.SenderClientId, out GameObject playerObject)) return;

            Inventory inv = playerObject.GetComponent<Inventory>();
            ItemData item = LoadItemById(itemId.ToString());
            if (inv == null || item == null || EmptySlotCount() <= 0 || !HasMapping(item)) return;
            if (!inv.TryConsume(item, 1)) return;
            TryPlaceLocal(item);
        }

        private bool HasMapping(ItemData item)
        {
            if (item == null) return false;
            if (GetAutoDriedResult(item) != null) return true;
            if (dryMappings == null) return false;
            foreach (var m in dryMappings)
                if (m != null && m.freshItem == item) return true;
            return false;
        }

        private ItemData GetDriedResult(ItemData fresh)
        {
            ItemData auto = GetAutoDriedResult(fresh);
            if (auto != null) return auto;

            if (dryMappings == null) return fresh;
            foreach (var m in dryMappings)
                if (m != null && m.freshItem == fresh) return m.driedItem != null ? m.driedItem : fresh;
            return fresh;
        }

        private ItemData GetAutoDriedResult(ItemData source)
        {
            if (source == null || string.IsNullOrEmpty(source.itemId)) return null;

            switch (source.itemId)
            {
                case "raw_meat":
                case "cooked_meat":
                    return LoadItemById("dried_meat");
                case "fish":
                case "cooked_fish":
                    return LoadItemById("dried_fish");
                default:
                    return null;
            }
        }

        private ItemData LoadItemById(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (_itemCache == null) _itemCache = Resources.LoadAll<ItemData>(itemResourcePath);

            foreach (var item in _itemCache)
            {
                if (item != null && item.itemId == itemId)
                    return item;
            }

            return null;
        }

        private void ApplySlotVisual(int index, ItemData item)
        {
            if (slotVisualAnchors == null || index < 0 || index >= slotVisualAnchors.Length) return;
            var anchor = slotVisualAnchors[index];
            if (anchor == null) return;

            // Đơn giản hoá: bật/tắt icon 3D con đầu tiên gắn sẵn dưới anchor (nếu có), thay vì
            // Instantiate model động — tránh phụ thuộc thêm prefab-per-item chưa có trong scope Block 4.
            if (anchor.childCount > 0) anchor.GetChild(0).gameObject.SetActive(item != null);
        }

        public int EmptySlotCount()
        {
            int c = 0;
            foreach (var s in _slots) if (s.IsEmpty) c++;
            return c;
        }

        public int ReadySlotCount()
        {
            int c = 0;
            foreach (var s in _slots) if (s.IsDone) c++;
            return c;
        }

        // ===================== IInteractable =====================
        public string GetPrompt()
        {
            int ready = ReadySlotCount();
            if (ready > 0) return $"[E] Thu thịt khô ({ready} sẵn sàng)";
            if (EmptySlotCount() > 0) return $"[E] Treo thịt/cá ({_slots.Length - EmptySlotCount()}/{_slots.Length})";
            return $"Giá phơi ({_slots.Length - EmptySlotCount()}/{_slots.Length})";
        }

        public bool CanInteract() => true;

        public void Interact(GameObject interactor)
        {
            var inv = interactor.GetComponent<Inventory>();
            EquipmentController equipment = interactor.GetComponent<EquipmentController>();
            ItemData equipped = equipment != null ? equipment.EquippedItem : null;

            if (equipped != null && HasMapping(equipped) && EmptySlotCount() > 0 && inv != null)
            {
                if (NetworkWorldInteraction.ShouldRouteToServer(this))
                {
                    if (inv.TryConsume(equipped, 1)) TryPlaceLocal(equipped);
                    PlaceServerRpc(new FixedString64Bytes(NetworkWorldInteraction.ItemId(equipped)));
                }
                else if (inv.TryConsume(equipped, 1))
                {
                    TryPlaceLocal(equipped);
                }
                return;
            }

            TryHarvestFirstReady(inv);
        }

        public void OnFocus() { }
        public void OnLoseFocus() { }

        public string CapturePersistenceState()
        {
            var state = new RackPersistenceState();
            foreach (Slot slot in _slots)
            {
                state.slots.Add(new RackSlotState
                {
                    itemId = slot.item != null ? slot.item.itemId : string.Empty,
                    timer = slot.timer
                });
            }
            return JsonUtility.ToJson(state);
        }

        public void RestorePersistenceState(string json)
        {
            RackPersistenceState saved = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<RackPersistenceState>(json);
            if (_slots == null || _slots.Length != Mathf.Max(1, slotCount))
            {
                _slots = new Slot[Mathf.Max(1, slotCount)];
                for (int i = 0; i < _slots.Length; i++) _slots[i] = new Slot();
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                RackSlotState slotState = saved?.slots != null && i < saved.slots.Count ? saved.slots[i] : null;
                _slots[i].item = slotState != null ? LoadItemById(slotState.itemId) : null;
                _slots[i].timer = _slots[i].item != null ? Mathf.Clamp(slotState.timer, 0f, DryDurationSeconds) : 0f;
                ApplySlotVisual(i, _slots[i].item);
            }
            OnRackChanged?.Invoke();
            PublishNetworkState();
        }

        private void PublishNetworkState()
        {
            if (IsServer) _networkState?.PublishNow();
        }
    }
}
