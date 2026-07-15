using UnityEngine;

namespace TheForest.Items
{
    public enum ItemCategory { Resource, Weapon, Tool, Food, Medical, Special }

    /// <summary>
    /// Định nghĩa một loại vật phẩm dưới dạng ScriptableObject.
    /// </summary>
    [CreateAssetMenu(fileName = "Item_", menuName = "The Forest/Item")]
    public class ItemData : ScriptableObject
    {
        [Header("Định danh")]
        [Tooltip("ID duy nhất, không đổi (vd 'stick', 'rock'). Dùng để khớp recipe & save.")]
        public string itemId;
        public string displayName;
        public ItemCategory category = ItemCategory.Resource;

        [Header("Hiển thị")]
        public Sprite icon;
        [TextArea] public string description;

        [Header("Stack")]
        [Tooltip("Số lượng tối đa trên 1 slot. Resource thường stack cao, vũ khí = 1.")]
        public int maxStack = 10;

        [Header("Hồi phục (nếu là Food/Medical)")]
        public float hungerRestore;
        public float thirstRestore;
        public float energyRestore;
        public float healthRestore;

        [Header("Trang bị (Equip)")]
        [Tooltip("Cho phép cầm trên tay (vũ khí, công cụ, đuốc).")]
        public bool isEquippable = false;
        [Tooltip("Prefab model hiển thị trên tay khi equip. Có thể để trống nếu chưa có model.")]
        public GameObject equipPrefab;

        [Header("Phòng thủ (Block)")]
        [Tooltip("% giảm sát thương khi block (0..1). GDD: Tier1=0, Club/Turtle=1.")]
        [Range(0f, 1f)] public float blockPercent = 0f;

        [Header("Block Pose (tư thế giơ chắn riêng vũ khí)")]
        [Tooltip("Offset vị trí model khi block (local, so với idle trên tay).")]
        public Vector3 blockPositionOffset = new Vector3(-0.12f, 0.08f, -0.05f);
        [Tooltip("Offset xoay model khi block (Euler local).")]
        public Vector3 blockRotationOffset = new Vector3(-20f, 25f, 35f);
        [Tooltip("Hệ số giảm tốc khi block với vũ khí này (1 = không giảm, 0.5 = còn nửa).")]
        [Range(0.1f, 1f)] public float blockMoveSpeedMultiplier = 0.6f;


    }
}