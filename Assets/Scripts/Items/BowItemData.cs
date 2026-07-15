using UnityEngine;
using TheForest.Items;

namespace TheForest.Items
{
    [CreateAssetMenu(fileName = "Bow_", menuName = "The Forest/Bow")]
    public class BowItemData : ItemData
    {
        [Header("Bắn")]
        [Tooltip("Lực bắn tối thiểu (kéo nhẹ) -> tối đa (kéo căng).")]
        public float minLaunchSpeed = 12f;
        public float maxLaunchSpeed = 40f;
        [Tooltip("Thời gian kéo full charge (giây).")]
        public float drawTime = 0.9f;
        [Tooltip("Damage gốc cộng từ cung (cộng với arrow).")]
        public float bowDamage = 5f;
    }
}
