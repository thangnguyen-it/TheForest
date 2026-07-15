using UnityEngine;

namespace TheForest.World
{
    public enum DifficultyMode { Peaceful, Normal, Hard, HardSurvival }

    /// <summary>
    /// Bộ hệ số theo độ khó (GDD). Tạo asset cho từng mode qua Create > The Forest > Difficulty.
    /// </summary>
    [CreateAssetMenu(fileName = "Difficulty_", menuName = "The Forest/Difficulty")]
    public class DifficultySettings : ScriptableObject
    {
        public DifficultyMode mode = DifficultyMode.Normal;

        [Header("AI")]
        [Tooltip("Nhân tần suất tấn công AI (Hard 2.5).")]
        public float aiAttackChanceMult = 1f;
        [Tooltip("Nhân follow-up attack (Hard 2.5).")]
        public float aiFollowUpMult = 1f;
        [Tooltip("Nhân sát thương AI lên player.")]
        public float aiDamageMult = 1f;

        [Header("Lửa lên mutant")]
        [Tooltip("Nhân fire damage lên mutant (Hard 0.5 = kháng lửa hơn).")]
        public float fireDamageOnMutantMult = 1f;

        [Header("Knockdown")]
        [Tooltip("Nhân cơ hội knockdown từ heavy attack (Hard 0.25).")]
        public float knockdownChanceMult = 1f;

        [Header("Structure")]
        public float structureDamageMult = 1f;

        [Header("Stealth")]
        [Tooltip("Trần stealth (Normal 1.0 = 100%, Hard 0.5 = 50%).")]
        [Range(0f, 1f)] public float maxStealth = 1f;

        [Header("Quả độc")]
        [Tooltip("Sát thương mỗi snowberry/twinberry (Normal 1, Hard 30).")]
        public float poisonBerryDamage = 1f;

        [Header("Hồi/khan hiếm (tùy chọn)")]
        public float animalSpawnMult = 1f;
        public float healthRegenMult = 1f;
    }
}
