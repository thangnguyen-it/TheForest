using UnityEngine;
using TheForest.Items;

namespace TheForest.Items
{
    public enum ArrowType { Normal, Poison, Fire }

    [CreateAssetMenu(fileName = "Arrow_", menuName = "The Forest/Arrow")]
    public class ArrowItemData : ItemData
    {
        public ArrowType arrowType = ArrowType.Normal;
        [Tooltip("Damage trúng trực tiếp của tên.")]
        public float arrowDamage = 20f;

        [Header("Poison")]
        [Tooltip("DoT poison mỗi giây.")]
        public float poisonDps = 3f;
        public float poisonDuration = 5f;
        [Tooltip("Làm chậm mục tiêu (0..1, 0.5 = còn nửa tốc).")]
        [Range(0f, 1f)] public float slowFactor = 0.5f;

        [Header("Fire")]
        [Tooltip("Prefab FireZone để lại khi trúng (tên lửa).")]
        public GameObject fireZonePrefab;

        [Header("Projectile")]
        public GameObject arrowProjectilePrefab; // model tên + Arrow.cs
    }
}
