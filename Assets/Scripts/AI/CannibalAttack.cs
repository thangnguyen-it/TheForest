using System.Collections;
using UnityEngine;
using TheForest.Player;

namespace TheForest.AI
{
    /// <summary>
    /// Điều phối đòn tấn công của cannibal: phát animation, áp damage GIỮA đòn
    /// (hit frame) thay vì tức thời -> player có thể né/chạy ra trước khi dính.
    /// Hỗ trợ ném projectile đạn lửa tầm xa cho biến thể Fire Thrower.
    /// Đã tích hợp hệ số Độ Khó (GameDifficulty).
    /// </summary>
    public class CannibalAttack : MonoBehaviour
    {
        [Header("Tham chiếu cận chiến")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform attackOrigin; // điểm gốc đòn (tay/ngực)

        [Header("Thông số đòn cận chiến")]
        [SerializeField] private float damage = 12f;
        [SerializeField] private float attackRange = 2.2f;
        [Tooltip("Bán kính bao quanh điểm đánh để tính trúng (rộng hơn point).")]
        [SerializeField] private float hitRadius = 1.2f;
        [SerializeField] private float cooldown = 1.5f;

        [Header("Fire Thrower (Đòn tầm xa)")]
        [SerializeField] private bool isRanged = false;
        [SerializeField] private GameObject fireProjectilePrefab;
        [SerializeField] private Transform throwOrigin;     // tay/đầu để bắn
        [SerializeField] private float projectileSpeed = 16f;
        [SerializeField] private float rangedDamage = 10f;
        [Tooltip("Tầm bắn tối đa.")]
        [SerializeField] private float rangedRange = 18f;

        [Header("Timing (fallback khi không dùng Animation Event)")]
        [Tooltip("Bật nếu CLIP có Animation Event gọi OnAttackHitFrame. Tắt -> dùng timing.")]
        [SerializeField] private bool useAnimationEvent = false;
        [Tooltip("Thời lượng 1 đòn (giây) khi dùng timing.")]
        [SerializeField] private float attackDuration = 1f;
        [Tooltip("Thời điểm hit (0..1) khi dùng timing.")]
        [Range(0f, 1f)][SerializeField] private float hitNormalized = 0.45f;

        [Header("SFX")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] attackSfx;
        [SerializeField] private AudioClip[] hitSfx;

        [Header("Loại địch")]
        [Tooltip("Creepy Mutant (Armsy/Virginia/Cowman): perfect block không đẩy lùi được.")]
        [SerializeField] private bool isCreepyMutant = false;

        private static readonly int AttackHash = Animator.StringToHash("Attack");

        private float _cooldownTimer;
        private bool _isAttacking;
        private Transform _target;
        private SurvivalStats _targetStats;

        public bool IsAttacking => _isAttacking;
        public bool IsRanged => isRanged;
        public float RangedRange => rangedRange;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (attackOrigin == null) attackOrigin = transform;
        }

        private void Update()
        {
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;
        }

        public bool CanAttack() => !_isAttacking && _cooldownTimer <= 0f;

        /// <summary>CannibalAI gọi khi player trong tầm. Trả true nếu khởi động đòn.</summary>
        public bool TryAttack(Transform target, SurvivalStats targetStats)
        {
            if (!CanAttack() || target == null) return false;

            _target = target;
            _targetStats = targetStats;
            _isAttacking = true;

            // ==========================================
            // TÍCH HỢP ĐỘ KHÓ: TẦN SUẤT ĐÁNH
            // ==========================================
            float chanceMult = TheForest.World.GameDifficulty.AiAttackChance;
            _cooldownTimer = cooldown / Mathf.Max(0.1f, chanceMult);  // Hard 2.5 -> đánh dồn dập hơn

            PlayRandom(attackSfx);

            if (animator != null) animator.SetTrigger(AttackHash);

            if (useAnimationEvent)
            {
                StartCoroutine(EndAfter(attackDuration));
            }
            else
            {
                StartCoroutine(TimedHitRoutine());
            }
            return true;
        }

        // ===================== HIT =====================

        public void OnAttackHitFrame()
        {
            if (!_isAttacking) return;
            ResolveHit();
        }

        private IEnumerator TimedHitRoutine()
        {
            float hitTime = attackDuration * hitNormalized;
            yield return new WaitForSeconds(hitTime);
            ResolveHit();
            yield return new WaitForSeconds(Mathf.Max(0f, attackDuration - hitTime));
            _isAttacking = false;
        }

        private IEnumerator EndAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            _isAttacking = false;
        }

        /// <summary>
        /// Rẽ nhánh xử lý đòn đánh: Ném bóng lửa (nếu isRanged) HOẶC tính sát thương cận chiến.
        /// </summary>
        private void ResolveHit()
        {
            if (_target == null) return;

            if (isRanged)
            {
                ThrowFireball();
                return;
            }

            Vector3 origin = attackOrigin.position;
            float dist = Vector3.Distance(origin, _target.position);
            if (dist > attackRange + hitRadius) return; // né thành công

            PlayRandom(hitSfx);

            var dmgable = _target.GetComponent<TheForest.Interaction.IDamageable>();
            if (dmgable != null)
            {
                Vector3 hitDir = (_target.position - origin); hitDir.y = 0f; hitDir.Normalize();

                // ==========================================
                // TÍCH HỢP ĐỘ KHÓ: SÁT THƯƠNG CẬN CHIẾN
                // ==========================================
                float finalDmg = damage * TheForest.World.GameDifficulty.AiDamage;
                dmgable.DealDamage(finalDmg, hitDir, transform, isCreepyMutant);
            }
            else if (_targetStats != null)
            {
                float finalDmg = damage * TheForest.World.GameDifficulty.AiDamage;
                _targetStats.ApplyDamage(finalDmg); // fallback nếu player chưa có receiver
            }
        }

        private void ThrowFireball()
        {
            if (fireProjectilePrefab == null) return;
            Transform origin = throwOrigin != null ? throwOrigin : attackOrigin;

            // Tính vận tốc bắn theo cung tới vị trí player (ballistic đơn giản)
            Vector3 to = _target.position + Vector3.up * 1f - origin.position;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            float dist = flat.magnitude;

            Vector3 dir = (to).normalized;
            // bù cung nhẹ theo khoảng cách
            Vector3 launchDir = (dir + Vector3.up * Mathf.Clamp01(dist / rangedRange) * 0.5f).normalized;

            var go = Instantiate(fireProjectilePrefab, origin.position, Quaternion.LookRotation(launchDir));
            var proj = go.GetComponent<FireProjectile>();

            if (proj != null)
            {
                // ==========================================
                // TÍCH HỢP ĐỘ KHÓ: SÁT THƯƠNG TẦM XA
                // ==========================================
                float finalRangedDmg = rangedDamage * TheForest.World.GameDifficulty.AiDamage;
                proj.Launch(launchDir * projectileSpeed, finalRangedDmg, transform.root);
            }

            PlayRandom(hitSfx);
        }

        private void PlayRandom(AudioClip[] clips)
        {
            if (audioSource == null || clips == null || clips.Length == 0) return;
            var c = clips[Random.Range(0, clips.Length)];
            if (c != null) audioSource.PlayOneShot(c);
        }

        public void CancelAttack()
        {
            StopAllCoroutines();
            _isAttacking = false;
        }

        public float AttackRange => attackRange;

        // ===================== HELPER CHO SPAWNER =====================
        public void SetCreepy(bool v) => isCreepyMutant = v;
    }
}