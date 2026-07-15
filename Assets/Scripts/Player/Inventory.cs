using System;
using System.Collections.Generic;
using UnityEngine;
using TheForest.Items;

namespace TheForest.Player
{
    [Serializable]
    public class InventorySlot
    {
        public ItemData item;
        public int count;

        public bool IsEmpty => item == null || count <= 0;
        public bool IsFull => item != null && count >= item.maxStack;
    }

    /// <summary>
    /// Túi đồ người chơi: thêm/bớt vật phẩm, stack theo maxStack, đếm số lượng.
    /// Crafting/Building sẽ truy vấn GetCount() và TryConsume() để kiểm tra nguyên liệu.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        [Header("Cấu hình")]
        [SerializeField] private int slotCount = 24;

        [SerializeField] private List<InventorySlot> _slots = new List<InventorySlot>();

        public IReadOnlyList<InventorySlot> Slots => _slots;

        public event Action OnInventoryChanged;

        private void Awake()
        {
            if (_slots == null) _slots = new List<InventorySlot>();

            // Giữ lại các vật phẩm gán sẵn để test, chỉ thêm slot trống cho đủ slotCount
            while (_slots.Count < slotCount)
            {
                _slots.Add(new InventorySlot());
            }
        }
        private void Start()
        {
            var equipment = GetComponent<EquipmentController>();
            if (equipment != null)
            {
                OnInventoryChanged += equipment.ValidateEquipped;
            }
        }
        /// <summary>Thêm vật phẩm. Trả về số lượng KHÔNG nhét được (0 = nhét hết).</summary>
        public int Add(ItemData item, int amount)
        {
            if (item == null || amount <= 0) return amount;

            // 1) Dồn vào các slot cùng loại chưa đầy
            foreach (var slot in _slots)
            {
                if (amount <= 0) break;
                if (!slot.IsEmpty && IsSameItem(slot.item, item) && !slot.IsFull)
                {
                    int space = slot.item.maxStack - slot.count;
                    int added = Mathf.Min(space, amount);
                    slot.count += added;
                    amount -= added;
                }
            }

            // 2) Đổ phần còn lại vào slot rỗng
            foreach (var slot in _slots)
            {
                if (amount <= 0) break;
                if (slot.IsEmpty)
                {
                    int added = Mathf.Min(item.maxStack, amount);
                    slot.item = item;
                    slot.count = added;
                    amount -= added;
                }
            }

            OnInventoryChanged?.Invoke();
            return amount; // >0 nghĩa là túi đầy, còn dư
        }

        /// <summary>Đếm tổng số lượng một loại item trên tất cả slot.</summary>
        public int GetCount(ItemData item)
        {
            if (item == null) return 0;
            int total = 0;
            foreach (var slot in _slots)
                if (!slot.IsEmpty && IsSameItem(slot.item, item)) total += slot.count;
            return total;
        }

        public bool Has(ItemData item, int amount) => GetCount(item) >= amount;

        /// <summary>Tiêu thụ số lượng item (cho craft/build). Trả false nếu không đủ.</summary>
        public bool TryConsume(ItemData item, int amount)
        {
            if (!Has(item, amount)) return false;

            int remaining = amount;
            foreach (var slot in _slots)
            {
                if (remaining <= 0) break;
                if (!slot.IsEmpty && IsSameItem(slot.item, item))
                {
                    int removed = Mathf.Min(slot.count, remaining);
                    slot.count -= removed;
                    remaining -= removed;
                    if (slot.count <= 0) { slot.item = null; slot.count = 0; }
                }
            }

            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool IsFull()
        {
            foreach (var slot in _slots)
                if (slot.IsEmpty || !slot.IsFull) return false;
            return true;
        }

        /// <summary>Clears all slots. Used by authoritative save/load and inventory replication.</summary>
        public void Clear()
        {
            foreach (var slot in _slots)
            {
                slot.item = null;
                slot.count = 0;
            }

            OnInventoryChanged?.Invoke();
        }

        private static bool IsSameItem(ItemData a, ItemData b)
        {
            if (a == b) return true;
            if (a == null || b == null) return false;
            if (string.IsNullOrEmpty(a.itemId) || string.IsNullOrEmpty(b.itemId)) return false;
            return string.Equals(a.itemId, b.itemId, StringComparison.Ordinal);
        }
    }
}
