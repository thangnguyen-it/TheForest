using System.Collections.Generic;
using UnityEngine;
using TheForest.Items;

namespace TheForest.Crafting
{
    [System.Serializable]
    public class Ingredient
    {
        public ItemData item;
        public int amount = 1;
        [Tooltip("False cho dụng cụ dùng trong recipe nhưng không bị tiêu hao, vd Utility Knife/Can Opener.")]
        public bool consume = true;
    }

    /// <summary>
    /// Một công thức chế tạo. Tạo asset qua Create > The Forest > Crafting Recipe.
    /// Ví dụ Stone Axe: 1 Stick + 1 Rock + 1 Rope -> Stone Axe.
    /// </summary>
    [CreateAssetMenu(fileName = "Recipe_", menuName = "The Forest/Crafting Recipe")]
    public class CraftingRecipe : ScriptableObject
    {
        [Header("Nguyên liệu")]
        public List<Ingredient> ingredients = new List<Ingredient>();

        [Header("Kết quả")]
        public ItemData result;
        public int resultAmount = 1;

        [Header("Thông tin")]
        [Tooltip("Có phải nâng cấp vũ khí không (để áp giới hạn 30 lần/vũ khí sau này)")]
        public bool isUpgrade = false;
    }
}
