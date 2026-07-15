using System;
using UnityEngine;
using TheForest.Interaction;

namespace TheForest.AI
{
    /// <summary>
    /// Máu + chết của Puffy/Blind Mutant. Cài CẢ IChoppable (đòn player qua WeaponSwinger/Arrow) VÀ
    /// IDamageable (đòn cannibal qua CannibalAttack) — giống VirginiaHealth, vì Mutant cũng là mục
    /// tiêu hợp lệ của cả 2 luồng damage trong dự án (khác Cannibal/Animal chỉ cần IChoppable).
    ///
    /// FIX fidelity (#3): máu Puffy MẶC ĐỊNH THẤP HƠN Cannibal thường (60) — wiki chính thức xác nhận
    /// Puffy/Blind Mutant "notably weaker and more sluggish compared to other cannibals". Bản cũ để 90
    /// (cao hơn cannibal) là SAI, dựa nguồn không khớp wiki. Đặt 40 (dưới cannibal 60, trên thú yếu).
    /// Biến thể Tough (Spotted/Blue) mới khoẻ lên qua toughHealthMultiplier ở MutantAI.
    /// </summary>
    [RequireComponent(typeof(MutantAI))]
    public class MutantHealth : MonoBehaviour, IChoppable, IDamageable
    {
        [Header("Máu (thấp hơn Cannibal 60 — Puffy yếu hơn cannibal)")]
        [SerializeField] private float maxHealth = 40f;
        [SerializeField] private float health = 40f;

        [Header("Xác (tuỳ chọn)")]
        [Tooltip("Có thể tái dùng prefab CannibalCorpse (Kind=Animal) để Starving ăn được, hoặc để trống. " +
                 "Harvest ra Creepy Armor (GDD thật đã xác nhận) thuộc Giai đoạn 3 của roadmap — chưa xử lý ở đây.")]
        [SerializeField] private GameObject corpsePrefab;

        public bool IsDead { get; private set; }
        public float HealthNormalized => health / Mathf.Max(1f, maxHealth);

        public event Action OnDeathEvent;

        private MutantAI _ai;

        private void Awake()
        {
            _ai = GetComponent<MutantAI>();
            health = maxHealth;
        }

        // ===================== IChoppable (đòn của PLAYER) =====================
        public bool CanBeChopped() => !IsDead;

        public bool ApplyChop(float damage, Transform attacker) => TakeDamage(damage, attacker);

        // ===================== IDamageable (đòn của cannibal khác) =====================
        public void DealDamage(float amount, Vector3 hitDirection, Transform attacker, bool isCreepyMutant)
            => TakeDamage(amount, attacker);

        private bool TakeDamage(float amount, Transform attacker)
        {
            if (IsDead || amount <= 0f) return false;

            health = Mathf.Max(0f, health - amount);
            _ai.OnHurt(attacker);

            if (health <= 0f) { Die(); return true; }
            return false;
        }

        private void Die()
        {
            IsDead = true;
            OnDeathEvent?.Invoke();
            _ai.OnDeath();

            if (corpsePrefab != null)
                Instantiate(corpsePrefab, transform.position, transform.rotation);

            Destroy(gameObject, 0.2f);
        }

        /// <summary>MutantAI.SetToughVariant gọi hàm này khi roll trúng biến thể Blue/Spotted/Glowing.</summary>
        public void ScaleMaxHealth(float mult)
        {
            maxHealth *= Mathf.Max(0.1f, mult);
            health = maxHealth;
        }
    }
}
