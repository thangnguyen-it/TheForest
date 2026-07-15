using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TheForest.Player;
using TheForest.Crafting;

namespace TheForest.UI
{
    /// <summary>
    /// Giao diện túi đồ. Mở/đóng bằng phím I.
    /// Kết nối sự kiện chuột phải của từng slot sang hệ thống chế tạo.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("Nguồn dữ liệu")]
        [SerializeField] private Inventory inventory;
        [SerializeField] private PlayerLook playerLook;
        [SerializeField] private CraftingSystem craftingSystem;

        [Header("UI")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform slotGridParent;
        [SerializeField] private InventorySlotUI slotPrefab;

        [Header("Tùy chọn")]
        [SerializeField] private bool pauseWhileOpen = false;

        private readonly List<InventorySlotUI> _slotUIs = new List<InventorySlotUI>();
        public bool IsOpen { get; private set; }

        private void Start()
        {
            BuildSlots();
            if (panelRoot != null) panelRoot.SetActive(false);
            IsOpen = false;

            if (inventory != null)
                inventory.OnInventoryChanged += RefreshAll;
        }

        private void OnDestroy()
        {
            if (inventory != null)
                inventory.OnInventoryChanged -= RefreshAll;
        }

        public void OnToggleInventory(InputValue value)
        {
            if (value.isPressed) Toggle();
        }

        public void Toggle()
        {
            IsOpen = !IsOpen;
            if (panelRoot != null) panelRoot.SetActive(IsOpen);

            if (playerLook != null)
                playerLook.LookEnabled = !IsOpen;

            if (IsOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                RefreshAll();
                if (pauseWhileOpen) Time.timeScale = 0f;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                if (pauseWhileOpen) Time.timeScale = 1f;

                // Tự động hoàn trả nguyên liệu về túi khi người chơi đóng ba lô
                if (craftingSystem != null)
                    craftingSystem.ClearCraftArea();
            }
        }

        private void BuildSlots()
        {
            if (inventory == null || slotGridParent == null || slotPrefab == null) return;

            foreach (var slot in inventory.Slots)
            {
                var ui = Instantiate(slotPrefab, slotGridParent);

                ui.OnRightClick = (item) =>
                {
                    // Ưu tiên 1: Nếu là đồ trang bị được -> Rút lên tay cầm
                    if (item.isEquippable)
                    {
                        if (craftingSystem != null) craftingSystem.ClearCraftArea(); // Dọn bàn chế
                        var equipment = playerLook.GetComponent<EquipmentController>();
                        if (equipment != null) equipment.Equip(item);
                    }
                    // Ưu tiên 2: Nếu là tài nguyên thường -> Đẩy vào bàn chế tạo
                    else if (craftingSystem != null)
                    {
                        craftingSystem.AddToCraftArea(item);
                    }
                };

                _slotUIs.Add(ui);
            }
        }

        private void RefreshAll()
        {
            var slots = inventory.Slots;
            for (int i = 0; i < _slotUIs.Count && i < slots.Count; i++)
                _slotUIs[i].Refresh(slots[i]);
        }
    }
}