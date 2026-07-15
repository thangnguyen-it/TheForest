using System.Collections.Generic;
using UnityEngine;
using TheForest.Items;
using TheForest.Player;

namespace TheForest.Crafting
{
    /// <summary>
    /// Hệ thống chế tạo. Người chơi đưa nguyên liệu vào "khu craft" (CraftingArea);
    /// hệ thống tìm recipe khớp chính xác -> cho phép craft -> trừ nguyên liệu, thêm kết quả.
    /// Dùng Inventory.GetCount/TryConsume làm nguồn dữ liệu.
    /// </summary>
    public class CraftingSystem : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private Inventory inventory;
        [Tooltip("Tất cả recipe khả dụng (kéo các asset Recipe_ vào đây)")]
        [SerializeField] private List<CraftingRecipe> allRecipes = new List<CraftingRecipe>();
        [SerializeField] private bool loadSotFRecipesFromResources = true;
        [SerializeField] private string sotFRecipesResourcePath = "SotFData/Recipes";

        // Khu craft: item -> số lượng người chơi đã đưa vào
        private readonly Dictionary<ItemData, int> _craftArea = new Dictionary<ItemData, int>();

        public IReadOnlyDictionary<ItemData, int> CraftArea => _craftArea;
        public CraftingRecipe CurrentMatch { get; private set; }

        // Event cho UI: khu craft đổi / có recipe khớp (hiện bánh răng) / craft xong
        public event System.Action OnCraftAreaChanged;
        public event System.Action<CraftingRecipe> OnRecipeMatched;   // null = không khớp
        public event System.Action<CraftingRecipe> OnCrafted;

        private void Awake()
        {
            if (inventory == null) inventory = GetComponent<Inventory>();
            LoadResourceRecipes();
        }

        private void LoadResourceRecipes()
        {
            if (!loadSotFRecipesFromResources) return;

            var recipes = Resources.LoadAll<CraftingRecipe>(sotFRecipesResourcePath);
            foreach (var recipe in recipes)
            {
                if (recipe != null && !allRecipes.Contains(recipe))
                    allRecipes.Add(recipe);
            }
        }

        /// <summary>Đưa 1 nguyên liệu từ túi vào khu craft (chuột phải lên item).</summary>
        public bool AddToCraftArea(ItemData item)
        {
            if (item == null) return false;

            int inArea = _craftArea.TryGetValue(item, out int c) ? c : 0;
            // Chỉ đưa vào được nếu túi còn (ngoài phần đã ở khu craft)
            if (inventory.GetCount(item) - inArea <= 0) return false;

            _craftArea[item] = inArea + 1;
            EvaluateMatch();
            OnCraftAreaChanged?.Invoke();
            return true;
        }

        /// <summary>Lấy 1 nguyên liệu ra khỏi khu craft.</summary>
        public void RemoveFromCraftArea(ItemData item)
        {
            if (item == null || !_craftArea.ContainsKey(item)) return;
            _craftArea[item]--;
            if (_craftArea[item] <= 0) _craftArea.Remove(item);
            EvaluateMatch();
            OnCraftAreaChanged?.Invoke();
        }

        /// <summary>Trả toàn bộ nguyên liệu khu craft về (khi đóng UI / hủy).</summary>
        public void ClearCraftArea()
        {
            _craftArea.Clear();
            CurrentMatch = null;
            OnRecipeMatched?.Invoke(null);
            OnCraftAreaChanged?.Invoke();
        }

        /// <summary>So khu craft với mọi recipe, tìm recipe khớp CHÍNH XÁC.</summary>
        private void EvaluateMatch()
        {
            CurrentMatch = null;

            foreach (var recipe in allRecipes)
            {
                if (MatchesExactly(recipe))
                {
                    CurrentMatch = recipe;
                    break;
                }
            }
            OnRecipeMatched?.Invoke(CurrentMatch); // UI hiện/ẩn bánh răng
        }

        private bool MatchesExactly(CraftingRecipe recipe)
        {
            // Số loại nguyên liệu phải bằng nhau (khớp chính xác, không thừa)
            if (recipe.ingredients.Count != _craftArea.Count) return false;

            foreach (var ing in recipe.ingredients)
            {
                if (ing.item == null) return false;
                if (!_craftArea.TryGetValue(ing.item, out int have)) return false;
                if (have != ing.amount) return false;
            }
            return true;
        }

        /// <summary>Nhấn bánh răng: thực hiện craft nếu có recipe khớp.</summary>
        public bool Craft()
        {
            if (CurrentMatch == null) return false;

            // Kiểm tra & trừ nguyên liệu khỏi túi
            foreach (var ing in CurrentMatch.ingredients)
                if (!inventory.Has(ing.item, ing.amount)) return false;

            foreach (var ing in CurrentMatch.ingredients)
            {
                if (ing.consume)
                    inventory.TryConsume(ing.item, ing.amount);
            }

            // Thêm kết quả
            inventory.Add(CurrentMatch.result, CurrentMatch.resultAmount);

            var crafted = CurrentMatch;
            ClearCraftArea();
            OnCrafted?.Invoke(crafted);
            return true;
        }
    }
}
