using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Companion.Events;
using TheForest.Interaction;
using TheForest.World;

namespace TheForest.AI
{
    public enum MutantKind
    {
        Puffy // Blind Mutant — phổ biến nhất trong hang, mù, dò bằng nghe + "ngửi" tầm gần.
        // Fingers, Twins, MutantBaby, Demon... để dành cho Giai đoạn 2b/2c khi có nhu cầu mở rộng.
    }

    public enum MutantState { Dormant, Wander, Investigate, Attack, Stunned, Dead }

    /// <summary>
    /// AI cho Puffy / Blind Mutant (Giai đoạn 2 của roadmap) — mutant phổ biến nhất trong hang.
    /// Toàn bộ hành vi dưới đây đã tra cứu và kiểm chứng qua web search trước khi viết (không suy đoán):
    ///
    ///   - MÙ HOÀN TOÀN: không có viewRadius/viewAngle như CannibalAI — chỉ dò bằng NGHE (tái dùng
    ///     NoiseEventChannelSO Y HỆT AnimalAI/CannibalAI) và "cảm nhận" tầm cực gần (senseRadius nhỏ,
    ///     mô phỏng khứu giác/đụng chạm) khi đã lết tới đủ gần.
    ///   - Phần lớn thời gian ĐỨNG YÊN/QUỲ "ngủ", thỉnh thoảng lết vài bước rồi lại đứng — KHÔNG tuần
    ///     tra theo waypoint cố định như Cannibal.
    ///   - Phát hiện mục tiêu -> RÍT LỚN (Screech, qua NoiseSystem.EmitNoise loudness cao) để gọi Puffy
    ///     khác trong tầm nghe tới hỗ trợ — tái dùng ĐÚNG hạ tầng NoiseEventChannelSO có sẵn, không cần
    ///     một AggressionManager riêng cho Mutant.
    ///   - MỘT MÌNH thì HIẾM KHI dấn thân — thường lảng ra tìm đồng loại thay vì lao vào (đã kiểm chứng:
    ///     "solitary Puffies rarely attack on their own"). Mô phỏng qua CountNearbyAllies()+soloFleeChance.
    ///   - THÙ ĐỊCH với player VÀ cannibal thường. Tấn công cannibal qua IChoppable (CannibalHealth đã
    ///     implement sẵn). LƯU Ý GIỚI HẠN: CannibalAI.OnAttacked(attacker) hiện KHÔNG dùng attacker để
    ///     đổi mục tiêu (field `player` bị hardcode) — cannibal bị mutant đánh sẽ Stun/giật mình nhưng
    ///     CHƯA quay lại đánh đúng con mutant đó. Đây là giới hạn có sẵn của CannibalAI.cs, cần sửa
    ///     riêng (đổi sang mục tiêu chung) — NẰM NGOÀI phạm vi file này.
    ///   - MÁU CAO HƠN Cannibal thường (60) dù được xem là "yếu nhất trong các loại mutant" (tương đối
    ///     giữa các mutant, không phải so với cannibal) — đã kiểm chứng qua tra cứu.
    ///   - Biến thể Tough (Blue/Spotted/Glowing Puffy) có thêm đòn Lunge (lao nhanh) — game gốc có 2 đòn
    ///     mới (Spin + Lunge), bản này rút gọn còn Lunge để giữ phạm vi vừa phải.
    ///   - GIAI ĐOẠN 3: thêm Stun(duration) + trạng thái Stunned — Puffy đã kiểm chứng đặc biệt yếu
    ///     trước Stun Gun, dùng để liên kết với Bullet.cs/FirearmItemData.isStunWeapon.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class MutantAI : MonoBehaviour
    {
        [Header("Event Channels (Bus)")]
        [SerializeField] private NoiseEventChannelSO noiseChannel;

        [Header("Loài & biến thể")]
        [SerializeField] private MutantKind kind = MutantKind.Puffy;
        [Tooltip("Blue/Spotted/Glowing Puffy: máu/damage cao hơn + có đòn Lunge.")]
        [SerializeField] private bool isToughVariant = false;
        [SerializeField] private float toughHealthMultiplier = 1.8f;
        [SerializeField] private float toughDamageMultiplier = 1.4f;

        [Header("Mù — chỉ Nghe (xa) & Cảm nhận (rất gần)")]
        [SerializeField] private float hearingRadius = 20f;
        [Tooltip("Bán kính 'ngửi/đụng chạm' khi đã lết tới đủ gần — thay cho thị giác.")]
        [SerializeField] private float senseRadius = 3f;
        [SerializeField] private LayerMask senseMask = ~0;

        [Header("Dormant & Wander (phần lớn thời gian đứng/quỳ yên)")]
        [SerializeField] private float dormantMinTime = 4f;
        [SerializeField] private float dormantMaxTime = 12f;
        [SerializeField] private float wanderRadius = 6f;
        [SerializeField] private float wanderSpeed = 1.2f;

        [Header("Investigate")]
        [SerializeField] private float investigateSpeed = 2.5f;
        [SerializeField] private float loseInterestTime = 8f;

        [Header("Attack (vờn quanh mục tiêu — 'scattering, running circles')")]
        [SerializeField] private float attackSpeed = 6.5f;
        [SerializeField] private float attackRange = 1.8f;
        [SerializeField] private float attackDamage = 16f;
        [SerializeField] private float attackCooldown = 1.3f;
        [SerializeField] private float circleRadius = 3f;
        [SerializeField] private float circleAngleJitter = 45f;

        [Header("Lunge (chỉ Tough variant)")]
        [SerializeField] private float lungeRange = 5f;
        [SerializeField] private float lungeCooldown = 4f;
        [Range(0f, 1f)][SerializeField] private float lungeChance = 0.35f;
        [SerializeField] private float lungeSpeedMultiplier = 1.8f;

        [Header("Solo hiếm khi dấn thân (GDD: 'rarely attack on their own')")]
        [SerializeField] private float allySearchRadius = 15f;
        [SerializeField] private int minAlliesToCommit = 1;
        [Range(0f, 1f)][SerializeField] private float soloFleeChance = 0.7f;

        [Header("Screech (báo động đồng loại)")]
        [SerializeField] private float screechLoudness = 2.5f;

        [Header("Animator (tuỳ chọn)")]
        [SerializeField] private Animator locomotionAnimator;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int ScreechHash = Animator.StringToHash("Screech");
        private static readonly int LungeHash = Animator.StringToHash("Lunge");

        public MutantState State { get; private set; } = MutantState.Dormant;
        public MutantKind Kind => kind;

        private NavMeshAgent _agent;
        private MutantHealth _health;
        private Transform _target;
        private Vector3 _home;
        private Vector3 _investigatePoint;
        private float _dormantTimer;
        private float _investigateTimer;
        private float _attackTimer;
        private float _lungeTimer;
        private float _stunTimer;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<MutantHealth>();
            _home = transform.position;
            _dormantTimer = Random.Range(dormantMinTime, dormantMaxTime);
        }

        private void OnEnable()
        {
            if (noiseChannel != null) noiseChannel.Register(OnNoiseHeard);
            MutantRegistry.Register(this);
        }

        private void OnDisable()
        {
            if (noiseChannel != null) noiseChannel.Unregister(OnNoiseHeard);
            MutantRegistry.Unregister(this);
        }

        private void OnNoiseHeard(NoiseEvent e)
        {
            if (State == MutantState.Dead || State == MutantState.Attack || State == MutantState.Stunned) return;

            float radius = hearingRadius * e.Loudness;
            if ((e.Position - transform.position).sqrMagnitude > radius * radius) return;

            _investigatePoint = e.Position;
            SetState(MutantState.Investigate);
        }

        private void Update()
        {
            if (State == MutantState.Dead) return;

            if (State == MutantState.Stunned) { TickStunned(); return; } // khoá hoàn toàn — không cảm nhận/di chuyển

            if (_lungeTimer > 0f) _lungeTimer -= Time.deltaTime;

            // Cảm nhận tầm cực gần — kiểm tra ở MỌI trạng thái trừ Attack (đã có mục tiêu) / Dead / Stunned
            if (State != MutantState.Attack)
            {
                var sensed = FindNearbyTarget();
                if (sensed != null) OnTargetSensed(sensed);
            }

            switch (State)
            {
                case MutantState.Dormant: TickDormant(); break;
                case MutantState.Wander: TickWander(); break;
                case MutantState.Investigate: TickInvestigate(); break;
                case MutantState.Attack: TickAttack(); break;
            }

            if (locomotionAnimator != null) locomotionAnimator.SetFloat(SpeedParam, _agent.velocity.magnitude);
        }

        // ===================== CẢM NHẬN & QUYẾT ĐỊNH DẤN THÂN =====================
        private Transform FindNearbyTarget()
        {
            var hits = Physics.OverlapSphere(transform.position, senseRadius, senseMask, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.CompareTag("Player")) return h.transform;

                var cannibal = h.GetComponentInParent<CannibalAI>();
                if (cannibal != null && cannibal.State != CannibalState.Dead) return cannibal.transform;
            }
            return null;
        }

        private void OnTargetSensed(Transform sensed)
        {
            int allies = CountNearbyAllies(allySearchRadius);
            if (allies < minAlliesToCommit && Random.value < soloFleeChance)
            {
                // Solo & nhát: KHÔNG dấn thân — rít gọi đồng loại rồi lảng ra xa tìm hỗ trợ
                Screech();
                Vector3 away = transform.position - sensed.position; away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
                _investigatePoint = transform.position + away.normalized * 6f;
                SetState(MutantState.Investigate);
                return;
            }

            _target = sensed;
            Screech();
            SetState(MutantState.Attack);
        }

        private int CountNearbyAllies(float radius)
        {
            int count = 0;
            foreach (var m in MutantRegistry.All)
            {
                if (m == null || m == this || m.State == MutantState.Dead) continue;
                if ((m.transform.position - transform.position).sqrMagnitude <= radius * radius) count++;
            }
            return count;
        }

        private void Screech()
        {
            if (locomotionAnimator != null) locomotionAnimator.SetTrigger(ScreechHash);
            NoiseSystem.EmitNoise(transform.position, screechLoudness); // Puffy khác nghe thấy -> Investigate tới
        }

        // ===================== STATES =====================
        private void TickDormant()
        {
            if (_agent.isOnNavMesh) _agent.isStopped = true;
            _dormantTimer -= Time.deltaTime;
            if (_dormantTimer <= 0f)
            {
                if (_agent.isOnNavMesh) _agent.isStopped = false;
                SetState(MutantState.Wander);
            }
        }

        private void TickWander()
        {
            _agent.speed = wanderSpeed;
            if (!_agent.pathPending && _agent.remainingDistance <= 0.5f)
            {
                _dormantTimer = Random.Range(dormantMinTime, dormantMaxTime);
                SetState(MutantState.Dormant); // lết vài bước rồi lại đứng/quỳ — đúng hành vi thật
            }
        }

        private void TickInvestigate()
        {
            _agent.speed = investigateSpeed;
            _agent.SetDestination(_investigatePoint);

            _investigateTimer += Time.deltaTime;
            if (_investigateTimer >= loseInterestTime || (!_agent.pathPending && _agent.remainingDistance <= 1f))
                SetState(MutantState.Dormant);
        }

        private void TickAttack()
        {
            if (_target == null) { SetState(MutantState.Dormant); return; }

            float dist = Vector3.Distance(transform.position, _target.position);

            if (dist > attackRange)
            {
                bool lunging = isToughVariant && dist <= lungeRange && _lungeTimer <= 0f
                               && Random.value < lungeChance;
                if (lunging)
                {
                    _lungeTimer = lungeCooldown;
                    _agent.speed = attackSpeed * lungeSpeedMultiplier;
                    _agent.SetDestination(_target.position);
                    if (locomotionAnimator != null) locomotionAnimator.SetTrigger(LungeHash);
                }
                else
                {
                    _agent.speed = attackSpeed;
                    // Vờn quanh thay vì lao thẳng — đúng "scattering, running circles around opponents"
                    float angle = Random.Range(-circleAngleJitter, circleAngleJitter);
                    Vector3 fromTarget = transform.position - _target.position; fromTarget.y = 0f;
                    if (fromTarget.sqrMagnitude < 0.01f) fromTarget = transform.forward;
                    Vector3 circled = _target.position + Quaternion.Euler(0f, angle, 0f) * fromTarget.normalized * circleRadius;
                    _agent.SetDestination(dist > circleRadius * 1.5f ? _target.position : circled);
                }
            }
            else
            {
                if (_agent.isOnNavMesh) _agent.ResetPath();
                if (_attackTimer <= 0f)
                {
                    _attackTimer = attackCooldown;
                    float mult = isToughVariant ? toughDamageMultiplier : 1f;
                    float dmg = attackDamage * mult * GameDifficulty.AiDamage;
                    ApplyAttackDamage(_target, dmg);
                }
            }

            if (_attackTimer > 0f) _attackTimer -= Time.deltaTime;
        }

        private void TickStunned()
        {
            if (_agent.isOnNavMesh) _agent.isStopped = true;
            if (locomotionAnimator != null) locomotionAnimator.SetFloat(SpeedParam, 0f);

            _stunTimer -= Time.deltaTime;
            if (_stunTimer <= 0f)
            {
                if (_agent.isOnNavMesh) _agent.isStopped = false;
                SetState(MutantState.Dormant);
            }
        }

        private void ApplyAttackDamage(Transform target, float damage)
        {
            // Thứ tự thử giống hệt Arrow.cs: IChoppable trước (Cannibal/Virginia), IDamageable sau (Player/Virginia)
            var choppable = target.GetComponentInParent<IChoppable>();
            if (choppable != null && choppable.CanBeChopped())
            {
                choppable.ApplyChop(damage, transform);
                return;
            }

            var damageable = target.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                Vector3 dir = (target.position - transform.position).normalized;
                damageable.DealDamage(damage, dir, transform, false);
            }
        }

        // ===================== DI CHUYỂN =====================
        private void PickWanderDest()
        {
            Vector3 rnd = _home + Random.insideUnitSphere * wanderRadius; rnd.y = transform.position.y;
            if (NavMesh.SamplePosition(rnd, out var hit, 4f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        private void SetState(MutantState next)
        {
            if (State == next) return;
            State = next;

            if (next == MutantState.Wander) PickWanderDest();
            else if (next == MutantState.Investigate) _investigateTimer = 0f;
        }

        // ===================== API CHO MutantHealth / vũ khí Stun =====================
        public void OnHurt(Transform attacker)
        {
            if (State == MutantState.Dead || State == MutantState.Stunned) return;
            if (attacker != null) OnTargetSensed(attacker); // bị đánh = "cảm nhận" được kẻ tấn công ngay lập tức
        }

        public void OnDeath()
        {
            State = MutantState.Dead;
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
            enabled = false;
        }

        /// <summary>Gọi bởi Bullet.cs khi trúng đạn Stun Gun (Giai đoạn 3) — Puffy đã kiểm chứng đặc biệt yếu trước loại vũ khí này.</summary>
        public void Stun(float duration)
        {
            if (State == MutantState.Dead) return;
            _stunTimer = Mathf.Max(_stunTimer, duration);
            if (_agent.isOnNavMesh) _agent.ResetPath();
            SetState(MutantState.Stunned);
        }

        /// <summary>Gọi bởi MutantSpawner khi roll trúng biến thể Tough (Blue/Spotted/Glowing).</summary>
        public void SetToughVariant(bool tough)
        {
            isToughVariant = tough;
            if (tough && _health != null) _health.ScaleMaxHealth(toughHealthMultiplier);
        }
    }

    /// <summary>Registry tối giản (giống ClimbSpotRegistry/CorpseRegistry) để Puffy đếm đồng loại gần đó trước khi dấn thân.</summary>
    public static class MutantRegistry
    {
        private static readonly List<MutantAI> _all = new List<MutantAI>();
        public static IReadOnlyList<MutantAI> All => _all;

        public static void Register(MutantAI m) { if (m != null && !_all.Contains(m)) _all.Add(m); }
        public static void Unregister(MutantAI m) => _all.Remove(m);
    }
}