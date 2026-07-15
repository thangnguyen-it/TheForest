using UnityEngine;

namespace TheForest.World
{
    /// <summary>
    /// Giữ DifficultySettings hiện hành, truy cập toàn cục. Đặt 1 instance trong scene
    /// (hoặc bootstrap). Mọi hệ thống hỏi GameDifficulty.Current để lấy hệ số.
    /// </summary>
    public class GameDifficulty : MonoBehaviour
    {
        public static GameDifficulty Instance { get; private set; }

        [SerializeField] private DifficultySettings settings;

        public static DifficultySettings Current =>
            Instance != null ? Instance.settings : null;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Ưu tiên lựa chọn từ menu nếu có
            if (DifficultyCarrier.Selected != null)
                settings = DifficultyCarrier.Selected;
        }

        public void SetDifficulty(DifficultySettings s) => settings = s;

        // Helper an toàn (trả 1f nếu chưa cấu hình)
        public static float AiAttackChance => Current != null ? Current.aiAttackChanceMult : 1f;
        public static float AiDamage => Current != null ? Current.aiDamageMult : 1f;
        public static float FireOnMutant => Current != null ? Current.fireDamageOnMutantMult : 1f;
        public static float KnockdownChance => Current != null ? Current.knockdownChanceMult : 1f;
        public static float StructureDamage => Current != null ? Current.structureDamageMult : 1f;
        public static float MaxStealth => Current != null ? Current.maxStealth : 1f;
        public static float PoisonBerryDamage => Current != null ? Current.poisonBerryDamage : 1f;
    }
}