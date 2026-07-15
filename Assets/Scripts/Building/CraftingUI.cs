using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TheForest.Crafting;
using TheForest.Items;

namespace TheForest.UI
{
    /// <summary>
    /// Giao diện khu craft: hiện nguyên liệu đã đưa vào + nút bánh răng khi có recipe khớp.
    /// Chuột phải item trong túi -> AddToCraftArea (nối ở InventorySlotUI sau).
    /// </summary>
    public class CraftingUI : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private CraftingSystem crafting;

        [Header("UI")]
        [SerializeField] private Transform craftAreaParent;     // nơi hiện icon nguyên liệu đã đưa vào
        [SerializeField] private InventorySlotUI craftSlotPrefab;
        [SerializeField] private Button gearButton;             // nút bánh răng
        [SerializeField] private GameObject gearRoot;           // ẩn/hiện bánh răng

        private readonly List<InventorySlotUI> _areaSlots = new List<InventorySlotUI>();

        private void OnEnable()
        {
            if (crafting == null) return;
            crafting.OnCraftAreaChanged += RefreshArea;
            crafting.OnRecipeMatched += HandleRecipeMatched;
            if (gearButton != null) gearButton.onClick.AddListener(OnGearClicked);
        }

        private void OnDisable()
        {
            if (crafting == null) return;
            crafting.OnCraftAreaChanged -= RefreshArea;
            crafting.OnRecipeMatched -= HandleRecipeMatched;
            if (gearButton != null) gearButton.onClick.RemoveListener(OnGearClicked);
        }

        private void Start()
        {
            if (gearRoot != null) gearRoot.SetActive(false);
        }

        private void HandleRecipeMatched(CraftingRecipe recipe)
        {
            // Hiện bánh răng khi có recipe khớp chính xác
            if (gearRoot != null) gearRoot.SetActive(recipe != null);
        }

        private void OnGearClicked()
        {
            crafting.Craft();
        }

        private void RefreshArea()
        {
            // Xóa icon cũ, vẽ lại theo khu craft hiện tại
            foreach (var s in _areaSlots) Destroy(s.gameObject);
            _areaSlots.Clear();

            foreach (var kv in crafting.CraftArea)
            {
                var ui = Instantiate(craftSlotPrefab, craftAreaParent);
                // Dùng InventorySlot tạm để tái dùng Refresh
                ui.Refresh(new Player.InventorySlot { item = kv.Key, count = kv.Value });
                _areaSlots.Add(ui);
            }
        }
    }
}
