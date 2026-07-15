using System;
using UnityEngine;
using UnityEngine.InputSystem;
using TheForest.Items;
using UnityEngine.AI;

namespace TheForest.Player
{
    /// <summary>
    /// Hệ thống Block & Perfect Block nâng cao phía Player.
    /// Đáp ứng các quy định GDD: Chặn hướng trước, tốn stamina, phản đòn x2,
    /// động lực học theo lượng HP hiện tại, phạt dồn đòn liên tiếp.
    /// </summary>
    public class PlayerBlock : MonoBehaviour
    {
        [Header("Tham chiếu cốt lõi")]
        [SerializeField] private EquipmentController equipment;
        [SerializeField] private SurvivalStats stats;
        [SerializeField] private Transform body;

        [Header("Cấu hình Block")]
        [SerializeField] private float frontAngle = 100f;
        [SerializeField] private float staminaCostPerBlock = 12f;
        [Range(0f, 1f)][SerializeField] private float minBlockWhenHolding = 0.15f;

        [Header("Cấu hình Perfect Block")]
        [SerializeField] private float perfectWindow = 0.18f;
        [Range(0f, 1f)][SerializeField] private float perfectBlockPercent = 0.98f;
        [SerializeField] private float counterWindow = 2f;
        [SerializeField] private float counterDamageMult = 2f;
        [SerializeField] private float pushForce = 7f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip blockSfx;
        [SerializeField] private AudioClip perfectSfx;

        [Header("Hiệu quả theo HP (GDD: tốt nhất 60-10)")]
        [Tooltip("HP cao hơn mức này -> block yếu đi (>60).")]
        [SerializeField] private float hpUpperGood = 60f;
        [Tooltip("HP thấp hơn mức này -> block yếu đi (<10).")]
        [SerializeField] private float hpLowerGood = 10f;
        [Tooltip("Hệ số block khi HP > hpUpperGood (1 = không phạt).")]
        [Range(0.1f, 1f)][SerializeField] private float highHpEffectiveness = 0.7f;
        [Tooltip("Hệ số block khi HP < hpLowerGood (vùng nguy hiểm).")]
        [Range(0.1f, 1f)][SerializeField] private float lowHpEffectiveness = 0.5f;

        [Header("Giảm theo số đòn liên tiếp")]
        [Tooltip("Mỗi đòn block liên tiếp trừ bao nhiêu phần hiệu quả (0..1).")]
        [Range(0f, 0.5f)][SerializeField] private float perHitFalloff = 0.12f;
        [Tooltip("Hiệu quả tối thiểu còn lại dù bị đánh nhiều (sàn).")]
        [Range(0f, 1f)][SerializeField] private float minConsecutiveEffectiveness = 0.3f;
        [Tooltip("Bao lâu không bị đánh thì reset chuỗi (giây).")]
        [SerializeField] private float consecutiveResetTime = 1.5f;

        // runtime
        private int _consecutiveBlocks;
        private float _lastBlockedHitTime;

        // Runtime States
        public bool IsBlocking { get; private set; }
        private bool _isHoldingBlockButton;
        private float _blockPressedTime = -999f;
        private float _counterTimer;

        public bool CounterActive => _counterTimer > 0f;
        public float CounterMultiplier => CounterActive ? counterDamageMult : 1f;

        /// <summary>Hệ số tốc độ di chuyển khi đang block (theo vũ khí). 1 nếu không block.</summary>
        public float MoveSpeedMultiplier
        {
            get
            {
                if (!IsBlocking) return 1f;
                var item = equipment != null ? equipment.EquippedItem : null;
                return item != null ? item.blockMoveSpeedMultiplier : 0.6f; // fallback
            }
        }

        public event Action OnPerfectBlock;
        public event Action OnNormalBlock;

        private void Awake()
        {
            if (equipment == null) equipment = GetComponent<EquipmentController>();
            if (stats == null) stats = GetComponent<SurvivalStats>();
            if (body == null) body = transform;
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (_counterTimer > 0f) _counterTimer -= Time.deltaTime;

            // Mất vũ khí giữa chừng -> mất block
            if (IsBlocking && (equipment == null || !equipment.HasEquipped))
                IsBlocking = false;

            // Reset chuỗi đòn liên tiếp nếu lâu rồi không bị đánh
            if (_consecutiveBlocks > 0 && Time.time - _lastBlockedHitTime > consecutiveResetTime)
                _consecutiveBlocks = 0;
        }

        public void OnBlock(InputValue value)
        {
            _isHoldingBlockButton = value.isPressed;
            if (_isHoldingBlockButton)
            {
                // GDD: phải đang CẦM vũ khí mới block được
                if (equipment == null || !equipment.HasEquipped)
                {
                    IsBlocking = false;
                    return;
                }

                _blockPressedTime = Time.time;          // mốc tính perfect
                IsBlocking = HasStaminaToBlock();
            }
            else
            {
                IsBlocking = false;
            }
        }

        private bool HasStaminaToBlock() => stats != null && stats.StaminaCurrent > 0f;

        public float ProcessIncoming(float damage, Vector3 hitDirection,
                                     Transform attacker, bool isCreepyMutant)
        {
            if (!HasStaminaToBlock()) IsBlocking = false;
            if (!IsBlocking) return damage;

            // Chỉ chặn đòn phía trước
            Vector3 fwd = body.forward; fwd.y = 0f;
            Vector3 from = -hitDirection; from.y = 0f;
            float angle = Vector3.Angle(fwd, from.normalized);
            if (angle > frontAngle * 0.5f) return damage;

            bool perfect = (Time.time - _blockPressedTime) <= perfectWindow;

            // Cập nhật chuỗi đòn liên tiếp (mọi đòn bị block đều tính, kể cả perfect)
            _consecutiveBlocks++;
            _lastBlockedHitTime = Time.time;

            float blockPct;
            if (perfect)
            {
                // Perfect block: KHÔNG bị phạt theo HP/chuỗi (đỡ hoàn hảo)
                blockPct = perfectBlockPercent;
            }
            else
            {
                float baseBlock = Mathf.Max(minBlockWhenHolding, GetWeaponBlockPercent());
                // Áp hiệu quả theo HP và theo số đòn liên tiếp
                float effectiveness = GetHpEffectiveness() * GetConsecutiveEffectiveness();
                blockPct = Mathf.Clamp01(baseBlock * effectiveness);
            }

            float reduced = damage * (1f - blockPct);

            if (stats != null) stats.ConsumeStamina(staminaCostPerBlock);

            if (perfect)
            {
                _counterTimer = counterWindow;
                PlaySfx(perfectSfx);
                OnPerfectBlock?.Invoke();
                if (!isCreepyMutant && attacker != null) ExecuteKnockbackAndStagger(attacker, hitDirection);
            }
            else
            {
                PlaySfx(blockSfx);
                OnNormalBlock?.Invoke();
            }

            return reduced;
        }

        private float GetWeaponBlockPercent()
        {
            var item = equipment != null ? equipment.EquippedItem : null;
            return item != null ? item.blockPercent : 0f;
        }

        private float GetHpEffectiveness()
        {
            if (stats == null) return 1f;
            float hp = stats.HealthNormalized * 100f; // về thang 0..100

            if (hp >= hpLowerGood && hp <= hpUpperGood)
                return 1f; // vùng tối ưu

            if (hp > hpUpperGood)
            {
                // 60 -> 1, 100 -> highHpEffectiveness (nội suy tuyến tính)
                float t = Mathf.InverseLerp(hpUpperGood, 100f, hp);
                return Mathf.Lerp(1f, highHpEffectiveness, t);
            }

            // hp < hpLowerGood: 10 -> 1, 0 -> lowHpEffectiveness
            float t2 = Mathf.InverseLerp(hpLowerGood, 0f, hp);
            return Mathf.Lerp(1f, lowHpEffectiveness, t2);
        }

        private float GetConsecutiveEffectiveness()
        {
            float eff = 1f - perHitFalloff * _consecutiveBlocks;
            return Mathf.Max(minConsecutiveEffectiveness, eff);
        }

        private void ExecuteKnockbackAndStagger(Transform attacker, Vector3 hitDirection)
        {
            Vector3 pushDir = hitDirection; pushDir.y = 0f; pushDir.Normalize();

            // Xử lý giật lùi an toàn cho AI có NavMeshAgent trên Unity 6
            var agent = attacker.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled)
            {
                StartCoroutine(TemporaryDisableAgentForKnockback(agent, attacker.GetComponent<Rigidbody>(), pushDir));
            }
            else
            {
                var rb = attacker.GetComponent<Rigidbody>();
                if (rb != null) rb.AddForce(pushDir * pushForce, ForceMode.VelocityChange);
            }

            var ai = attacker.GetComponent<TheForest.AI.CannibalAI>();
            if (ai != null) ai.Stun(0.75f); // Tăng nhẹ thời gian Stun để người chơi kịp x2 damage
        }

        private System.Collections.IEnumerator TemporaryDisableAgentForKnockback(NavMeshAgent agent, Rigidbody rb, Vector3 direction)
        {
            agent.velocity = Vector3.zero;
            agent.isStopped = true;

            // Ép vận tốc vật lý giật lùi
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.AddForce(direction * pushForce, ForceMode.VelocityChange);
            }

            yield return new WaitForSeconds(0.25f);

            if (agent != null && agent.gameObject.activeInHierarchy)
            {
                if (rb != null) rb.isKinematic = true;
                agent.isStopped = false;
            }
        }

        private void PlaySfx(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }

        public void ConsumeCounter() => _counterTimer = 0f;
    }
}