using UnityEngine;

namespace TheForest.AI
{
    /// <summary>
    /// Cấu hình một biến thể cannibal (tribe + type) -> prefab + override chỉ số.
    /// Spawner chọn config phù hợp theo unlock/aggression. Tạo qua Create > The Forest > Cannibal Config.
    /// </summary>
    [CreateAssetMenu(fileName = "Cannibal_", menuName = "The Forest/Cannibal Config")]
    public class CannibalConfig : ScriptableObject
    {
        public CannibalTribe tribe = CannibalTribe.Regular;
        public CannibalType type = CannibalType.Male;

        [Tooltip("Prefab có CannibalAI + CannibalHealth + (CannibalAttack).")]
        public GameObject prefab;

        [Header("Trọng số chọn ngẫu nhiên trong nhóm")]
        [Min(0f)] public float spawnWeight = 1f;

        [Header("Ngày tối thiểu để xuất hiện")]
        public int minDay = 1;

        [Header("Override chỉ số (tùy chọn)")]
        public bool isCreepyMutant = false;   // Starving... thực ra không; để cho Armsy nếu tái dùng
        public float healthMultiplier = 1f;
        public float damageMultiplier = 1f;
        public float speedMultiplier = 1f;
    }
}
