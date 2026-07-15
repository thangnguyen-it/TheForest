using System;
using UnityEngine;
using TheForest.Interaction;
using Companion.Events;

namespace TheForest.AI
{
    [RequireComponent(typeof(CannibalAI))]
    public class CannibalHealth : MonoBehaviour, IChoppable
    {
        [Header("Event Channels")]
        [SerializeField] private AggressionEventChannelSO aggressionChannel;

        [Header("Máu")]
        [SerializeField] private float maxHealth = 60f;
        [SerializeField] private float health = 60f;

        [Header("Phản ứng đòn")]
        [SerializeField] private float stunDuration = 1f;
        [SerializeField] private float knockdownThreshold = 25f;
        [SerializeField] private float knockdownDuration = 2f;

        [Header("Stealth kill")]
        [SerializeField] private bool canBeStealthKilled = true;

        [Header("Xác (Corpse)")]
        [SerializeField] private GameObject corpsePrefab;

        public bool CanBeStealthKilled => canBeStealthKilled && !IsDead;

        private CannibalAI _ai;
        public bool IsDead { get; private set; }

        public event Action OnDeath;

        private void Awake()
        {
            _ai = GetComponent<CannibalAI>();
            health = maxHealth;
        }

        public bool CanBeChopped() => !IsDead;

        public bool TryStealthKill(Transform attacker)
        {
            if (!CanBeStealthKilled) return false;
            health = 0f;
            Die();
            return true;
        }

        public bool ApplyChop(float damage, Transform attacker)
        {
            if (IsDead) return false;

            health = Mathf.Max(0f, health - damage);

            if (health <= 0f)
            {
                Die();
                return true;
            }

            // ==========================================
            // TÍCH HỢP ĐỘ KHÓ B2b: TỶ LỆ KNOCKDOWN
            // ==========================================
            float knockMult = TheForest.World.GameDifficulty.KnockdownChance;
            bool doKnockdown = damage >= knockdownThreshold && UnityEngine.Random.value < knockMult;

            if (doKnockdown)
            {
                _ai.Knockdown(knockdownDuration);
            }
            else
            {
                _ai.Stun(stunDuration);
            }

            if (attacker != null) _ai.OnAttacked(attacker);
            return false;
        }

        public void HealAmount(float amount)
        {
            if (IsDead || amount <= 0f) return;
            health = Mathf.Min(maxHealth, health + amount);
        }

        private void Die()
        {
            IsDead = true;
            OnDeath?.Invoke();

            if (_ai != null)
            {
                int zone = _ai.ZoneId;
                aggressionChannel?.Raise(new AggressionDelta(zone, 0.4f));

                _ai.OnDeath();
                _ai.NotifyAllyDied(transform.position);
            }

            if (corpsePrefab != null)
            {
                Instantiate(corpsePrefab, transform.position, transform.rotation);
            }

            Destroy(gameObject, 0.2f);
        }

        public void ScaleMaxHealth(float mult)
        {
            maxHealth *= Mathf.Max(0.1f, mult);
            health = maxHealth;
        }
    }
}