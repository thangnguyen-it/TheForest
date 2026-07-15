using System;
using UnityEngine;
using TheForest.Interaction;

namespace TheForest.AI
{
    /// <summary>
    /// Máu + gục (Knockdown) + hồi (Revive) + chết vĩnh viễn của Virginia.
    ///
    /// GDD thật (đã tra cứu): Virginia có thể bị hạ gục bởi mutant/cannibal/player, hồi lại được trong
    /// lúc gục, nhưng nếu KHÔNG được hồi kịp hoặc bị giết hẳn thì mất VĨNH VIỄN — không hồi sinh lại
    /// trong save đó. Đây khớp gần như nguyên văn cơ chế permadeath đã có sẵn cho Kelvin
    /// (Companion.FSM.CompanionFSM: reviveWindowSeconds/revivePercent), nên class này TÁI DÙNG đúng 2
    /// khái niệm đó thay vì phát minh công thức mới, thay vì gắn Virginia vào CompanionFSM (vốn gắn chặt
    /// với CommandIssuedChannelSO/GatherableResourceDefinitionSO — những thứ Virginia không có, vì cô
    /// không nhận lệnh và không đi lấy tài nguyên).
    ///
    /// Cài CẢ HAI interface:
    ///   - IChoppable: đòn cận chiến CỦA PLAYER đi qua WeaponSwinger.RequestSwing (giống cây/thú/cannibal).
    ///   - IDamageable: đòn của CannibalAttack / Arrow / FireProjectile (giống Player).
    /// Virginia là mục tiêu hợp lệ của CẢ hai luồng damage trong dự án — khác cây/thú/cannibal (chỉ
    /// IChoppable) và khác Player (chỉ IDamageable) — nên cần implement cả hai để không bị "vô hình"
    /// trước một trong hai loại tấn công.
    /// </summary>
    [RequireComponent(typeof(VirginiaAI))]
    public class VirginiaHealth : MonoBehaviour, IChoppable, IDamageable
    {
        [Header("Máu")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float health = 100f;

        [Header("Gục & Hồi (khớp permadeath Kelvin — CompanionFSM)")]
        [SerializeField] private float reviveWindowSeconds = 10f;
        [Range(0f, 1f)][SerializeField] private float revivePercent = 0.5f;

        public bool IsKnockedDown { get; private set; }
        public bool IsDead { get; private set; }
        public float HealthNormalized => health / Mathf.Max(1f, maxHealth);

        public event Action OnKnockedDownEvent;
        public event Action OnRevivedEvent;
        public event Action OnDeathEvent;

        private VirginiaAI _ai;
        private float _reviveTimer;

        private void Awake()
        {
            _ai = GetComponent<VirginiaAI>();
            health = maxHealth;
        }

        private void Update()
        {
            if (!IsKnockedDown || IsDead) return;
            _reviveTimer -= Time.deltaTime;
            if (_reviveTimer <= 0f) Die();
        }

        // ===================== IChoppable (đòn cận chiến CỦA PLAYER) =====================
        public bool CanBeChopped() => !IsDead && !IsKnockedDown;

        public bool ApplyChop(float damage, Transform attacker)
        {
            bool byPlayer = attacker != null && attacker.root.CompareTag("Player");
            return TakeDamage(damage, byPlayer);
        }

        // ===================== IDamageable (đòn của cannibal/mutant/bẫy AI) =====================
        public void DealDamage(float amount, Vector3 hitDirection, Transform attacker, bool isCreepyMutant)
        {
            TakeDamage(amount, false);
        }

        // ===================== LÕI =====================
        private bool TakeDamage(float amount, bool byPlayer)
        {
            if (IsDead || IsKnockedDown || amount <= 0f) return false;

            health = Mathf.Max(0f, health - amount);

            if (byPlayer) _ai.OnAttackedByPlayer();
            else _ai.OnAttackedByEnemy();

            if (health <= 0f)
            {
                IsKnockedDown = true;
                _reviveTimer = reviveWindowSeconds;
                OnKnockedDownEvent?.Invoke();
                _ai.OnKnockedDown();
                return true;
            }
            return false;
        }

        /// <summary>Player nhấn E khi Virginia đang gục để hồi cô dậy (VirginiaAI.Interact() gọi hàm này).</summary>
        public bool TryRevive()
        {
            if (!IsKnockedDown || IsDead) return false;
            IsKnockedDown = false;
            health = maxHealth * revivePercent;
            OnRevivedEvent?.Invoke();
            _ai.OnRevived();
            return true;
        }

        private void Die()
        {
            IsDead = true;
            IsKnockedDown = false;
            OnDeathEvent?.Invoke();
            _ai.OnDeath(); // VĨNH VIỄN theo GDD thật — hệ Save/UI bên ngoài tự quyết định không respawn
        }
    }
}
