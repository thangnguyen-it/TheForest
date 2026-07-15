using System;
using UnityEngine;

namespace TheForest.Player
{
    /// <summary>
    /// Tàng hình player — MÔ HÌNH SONS OF THE FOREST THẬT (crouch + đứng yên), thay cho mô hình bùn/áo lá
    /// kiểu The Forest (2014) mà bản cũ dùng nhầm.
    ///
    /// FIX fidelity (#2): SotF KHÔNG có "bôi bùn để tàng hình", KHÔNG có "10 mảnh giáp lá = 100% ẩn".
    /// Ẩn mình thật trong SotF = NGỒI RÓN (crouch) + đi chậm/đứng yên trong bụi rậm. Vì vậy stealth ở đây
    /// tính từ SurvivalStats.IsCrouching (+ thưởng thêm khi đứng yên), KHÔNG còn cộng từ mud/armor/boots.
    ///
    /// GIỮ NGUYÊN TÊN LỚP + API công khai (NoiseMultiplier, EmitPlayerNoise, TotalStealth) để KHÔNG phá
    /// consumer sẵn có (WeaponSwinger.MakeHitNoise, CannibalAI đọc stealth). Chỉ NGUỒN của stealth đổi.
    /// Các API cũ ApplyMud/WashMud/SetArmorStealth/SetBootsStealth vẫn còn (no-op an toàn) để asset/scene
    /// cũ tham chiếu không lỗi — nhưng không còn tác dụng.
    /// </summary>
    public class PlayerMudCamo : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private SurvivalStats stats;

        [Header("Stealth theo tư thế (SotF thật)")]
        [Tooltip("Mức ẩn khi đang RÓN (crouch) và có di chuyển. 0..1.")]
        [Range(0f, 1f)][SerializeField] private float crouchStealth = 0.45f;
        [Tooltip("Cộng thêm khi RÓN mà ĐỨNG YÊN (nín thở trong bụi). 0..1.")]
        [Range(0f, 1f)][SerializeField] private float crouchStillBonus = 0.25f;

        [Header("Ẩn trong bụi rậm (SotF: đứng/ngồi trong bụi che tầm nhìn)")]
        [Tooltip("Layer của bụi/cây thấp có collider (đặt trigger). Để trống = tắt tính năng.")]
        [SerializeField] private LayerMask foliageMask = 0;
        [Tooltip("Bán kính kiểm tra thân người có nằm trong bụi không.")]
        [SerializeField] private float foliageCheckRadius = 0.6f;
        [Tooltip("Mức ẩn khi đứng trong bụi (rón trong bụi = cộng dồn với crouch).")]
        [Range(0f, 1f)][SerializeField] private float foliageStealth = 0.4f;

        [Header("Giới hạn")]
        [Tooltip("Trần tàng hình của component (còn bị kẹp thêm bởi GameDifficulty.MaxStealth).")]
        [Range(0f, 1f)][SerializeField] private float maxStealth = 0.9f;

        public event Action<float> OnStealthChanged;

        /// <summary>Player có đang đứng trong bụi rậm không (cập nhật mỗi frame).</summary>
        public bool InFoliage { get; private set; }

        private float _currentStealth;
        private float _lastStealth = -1f;

        /// <summary>Tổng stealth 0..1 (cache mỗi frame). Kẹp bởi maxStealth và GameDifficulty.MaxStealth.</summary>
        public float TotalStealth => _currentStealth;

        /// <summary>Hệ số nhân ĐỘ TO tiếng động (1 = bình thường, 0.5 = chỉ còn nửa).</summary>
        public float NoiseMultiplier => 1f - _currentStealth;

        private void Awake()
        {
            if (stats == null) stats = GetComponent<SurvivalStats>();
        }

        private void Update()
        {
            _currentStealth = ComputeStealth();
            if (!Mathf.Approximately(_currentStealth, _lastStealth))
            {
                _lastStealth = _currentStealth;
                OnStealthChanged?.Invoke(_currentStealth);
            }
        }

        private float ComputeStealth()
        {
            float s = 0f;

            // Nguồn 1: rón (crouch) — lõi tàng hình SotF.
            if (stats != null && stats.IsCrouching)
            {
                s += crouchStealth;
                if (stats.IsStill) s += crouchStillBonus;
            }

            // Nguồn 2: đứng/ngồi trong bụi rậm.
            InFoliage = foliageMask.value != 0 &&
                        Physics.CheckSphere(transform.position, foliageCheckRadius, foliageMask, QueryTriggerInteraction.Collide);
            if (InFoliage) s += foliageStealth;

            if (s <= 0f) return 0f; // đứng thẳng ngoài trảng trống = không ẩn (đúng SotF)

            float cap = Mathf.Min(maxStealth, TheForest.World.GameDifficulty.MaxStealth);
            return Mathf.Clamp(s, 0f, cap);
        }

        /// <summary>
        /// Gọi khi Player tạo ra tiếng ồn (chặt cây, đánh trúng...). Tự trừ độ giảm âm do đang rón.
        /// </summary>
        public void EmitPlayerNoise(float baseLoudness)
        {
            TheForest.AI.NoiseSystem.EmitNoise(transform.position, baseLoudness * NoiseMultiplier);
        }

        // ===================== API CŨ (no-op — giữ để không phá tham chiếu The-Forest cũ) =====================
        [Obsolete("SotF không có bùn tàng hình — no-op. Dùng crouch thay thế.")]
        public void ApplyMud(float amount) { }
        [Obsolete("SotF không có bùn tàng hình — no-op.")]
        public void WashMud() { }
        [Obsolete("SotF không có 'giáp lá = tàng hình' — no-op.")]
        public void SetArmorStealth(float v) { }
        [Obsolete("SotF không có 'giày thỏ = tàng hình' — no-op.")]
        public void SetBootsStealth(float v) { }
    }
}
