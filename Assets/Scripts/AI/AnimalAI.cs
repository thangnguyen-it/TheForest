using UnityEngine;
using UnityEngine.AI;
using TheForest.Player;
using Companion.Events;

namespace TheForest.AI
{
    public enum AnimalKind
    {
        Deer,
        Rabbit,
        Boar,
        Lizard,
        Raccoon,
        Turtle,
        Duck,
        Moose,
        Squirrel,
        Seagull,
        Bird,
        BlueBird,
        Hummingbird,
        Salmon,
        SeaTurtle,
        FreshwaterTurtle,
        Shark,
        Orca,
        Bat,
        Frog,
        Spider,
        Firefly,
        Starfish,
        BabyTurtle,
        Skunk
    }
    public enum AnimalState { Idle, Wander, Flee, Hostile, Dead }

    /// <summary>
    /// AI động vật theo GDD:
    /// - Deer/Rabbit/Lizard/Raccoon: nhút nhát, flee khi nghe tiếng động / bị đánh.
    /// - Boar: thù địch, tấn công player khi tới gần, đôi khi bỏ chạy rồi quay lại.
    /// Dùng NoiseSystem (đã có) để 'nghe' tiếng động player -> chạy.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class AnimalAI : MonoBehaviour
    {
        [Header("Event Channels (Bus)")]
        [SerializeField] private NoiseEventChannelSO noiseChannel;

        [Header("Loài")]
        [SerializeField] private AnimalKind kind = AnimalKind.Deer;

        [Header("Di chuyển")]
        [SerializeField] private float wanderRadius = 12f;
        [SerializeField] private float wanderSpeed = 2.5f;
        [SerializeField] private float fleeSpeed = 7f;
        [SerializeField] private float idleTimeRange = 3f;

        [Header("Nhát / nghe")]
        [Tooltip("Bán kính nghe tiếng động player -> hoảng chạy.")]
        [SerializeField] private float hearingRadius = 14f;
        [Tooltip("Bán kính thấy player -> nhút nhát bỏ chạy (Rabbit rất lớn).")]
        [SerializeField] private float playerScareRadius = 8f;
        [SerializeField] private float fleeDuration = 4f;

        [Header("Boar (thù địch)")]
        [SerializeField] private float boarAggroRange = 8f;
        [SerializeField] private float boarAttackRange = 2f;
        [SerializeField] private float boarDamage = 10f;
        [SerializeField] private float boarAttackCooldown = 2f;
        [Tooltip("Xác suất Boar bỏ chạy thay vì đánh khi bị thương.")]
        [Range(0f, 1f)][SerializeField] private float boarFleeChance = 0.4f;

        // ===================== RUNTIME =====================
        public AnimalState State { get; private set; } = AnimalState.Idle;
        private NavMeshAgent _agent;
        private Transform _player;
        private SurvivalStats _playerStats;
        private Vector3 _home;
        private float _idleTimer;
        private float _fleeTimer;
        private float _attackTimer;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _home = transform.position;
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) { _player = p.transform; _playerStats = p.GetComponent<SurvivalStats>(); }
        }

        private void OnEnable()
        {
            if (noiseChannel != null) noiseChannel.Register(OnNoiseHeard);
        }

        private void OnDisable()
        {
            if (noiseChannel != null) noiseChannel.Unregister(OnNoiseHeard);
        }

        private void OnNoiseHeard(NoiseEvent e)
        {
            HearNoise(e.Position, e.Loudness);
        }

        private void Update()
        {
            if (State == AnimalState.Dead) return;

            // Predator/defensive fauna chủ động phản ứng khi player tới gần.
            if ((IsPredator(kind) || IsDefensive(kind)) && _player != null
                && State != AnimalState.Flee
                && Vector3.Distance(transform.position, _player.position) < boarAggroRange)
            {
                State = AnimalState.Hostile;
            }

            switch (State)
            {
                case AnimalState.Idle: TickIdle(); break;
                case AnimalState.Wander: TickWander(); break;
                case AnimalState.Flee: TickFlee(); break;
                case AnimalState.Hostile: TickHostile(); break;
            }

            // Thấy player gần (loài nhát) -> chạy
            if (IsFlighty(kind) && _player != null && State != AnimalState.Flee)
            {
                if (Vector3.Distance(transform.position, _player.position) < playerScareRadius)
                    StartFlee(_player.position);
            }

            if (_attackTimer > 0f) _attackTimer -= Time.deltaTime;
        }

        // ===================== STATES =====================
        private void TickIdle()
        {
            _agent.speed = wanderSpeed;
            _idleTimer -= Time.deltaTime;
            if (_idleTimer <= 0f) { State = AnimalState.Wander; PickWanderDest(); }
        }

        private void TickWander()
        {
            if (!_agent.pathPending && _agent.remainingDistance <= 1f)
            {
                State = AnimalState.Idle;
                _idleTimer = Random.Range(idleTimeRange * 0.5f, idleTimeRange);
            }
        }

        private void TickFlee()
        {
            _agent.speed = fleeSpeed;
            _fleeTimer -= Time.deltaTime;
            if (_fleeTimer <= 0f)
            {
                State = AnimalState.Idle;
                _idleTimer = Random.Range(1f, idleTimeRange);
            }
            else if (!_agent.pathPending && _agent.remainingDistance <= 1f)
            {
                // tiếp tục chạy xa thêm
                FleeFrom(_player != null ? _player.position : transform.position);
            }
        }

        private void TickHostile()
        {
            if (_player == null) { State = AnimalState.Idle; return; }
            _agent.speed = fleeSpeed; // boar lao nhanh
            float d = Vector3.Distance(transform.position, _player.position);

            if (d > boarAggroRange * 1.5f) { State = AnimalState.Idle; return; }

            if (d > boarAttackRange)
            {
                _agent.SetDestination(_player.position);
            }
            else
            {
                if (_agent.isOnNavMesh) _agent.ResetPath();
                if (_attackTimer <= 0f)
                {
                    _attackTimer = boarAttackCooldown;
                    float dmg = boarDamage * TheForest.World.GameDifficulty.AiDamage;
                    var dmgable = _player.GetComponent<TheForest.Interaction.IDamageable>();
                    if (dmgable != null)
                    {
                        Vector3 dir = (_player.position - transform.position).normalized;
                        dmgable.DealDamage(dmg, dir, transform, false);
                    }
                    else if (_playerStats != null) _playerStats.ApplyDamage(dmg);
                }
            }
        }

        // ===================== FLEE HELPERS =====================
        private void StartFlee(Vector3 threat)
        {
            State = AnimalState.Flee;
            _fleeTimer = fleeDuration;
            FleeFrom(threat);
        }

        private void FleeFrom(Vector3 threat)
        {
            Vector3 away = (transform.position - threat); away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            Vector3 dest = transform.position + away.normalized * wanderRadius;
            if (NavMesh.SamplePosition(dest, out var hit, 6f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        private void PickWanderDest()
        {
            Vector3 rnd = _home + Random.insideUnitSphere * wanderRadius; rnd.y = transform.position.y;
            if (NavMesh.SamplePosition(rnd, out var hit, 4f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        // ===================== NGHE & BỊ ĐÁNH =====================

        /// <summary>NoiseSystem gọi khi player gây tiếng động (đã nhân bùn).</summary>
        public void HearNoise(Vector3 pos, float loudness)
        {
            if (State == AnimalState.Dead || State == AnimalState.Hostile) return;
            float radius = hearingRadius * loudness;
            if ((pos - transform.position).sqrMagnitude > radius * radius) return;
            StartFlee(pos); // nghe tiếng -> chạy xa nguồn
        }

        public void OnHurt(Transform attacker)
        {
            if (State == AnimalState.Dead) return;

            if (IsPredator(kind) || IsDefensive(kind))
            {
                // Defensive fauna đôi khi bỏ chạy, đôi khi nổi điên tấn công.
                if (Random.value < boarFleeChance && attacker != null)
                    StartFlee(attacker.position);
                else
                    State = AnimalState.Hostile;
            }
            else
            {
                // GDD: deer đứng yên khi bị melee (bug/feature) -> nhưng trúng tên thì chạy.
                // Đơn giản: bị đánh -> chạy (trừ khi bạn muốn deer đứng yên melee).
                if (attacker != null) StartFlee(attacker.position);
            }
        }

        public void OnDeath()
        {
            State = AnimalState.Dead;
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
            enabled = false;
        }

        public AnimalKind Kind => kind;

        public void ConfigureKind(AnimalKind newKind) => kind = newKind;

        private static bool IsPredator(AnimalKind k)
        {
            return k == AnimalKind.Shark || k == AnimalKind.Orca;
        }

        private static bool IsDefensive(AnimalKind k)
        {
            return k == AnimalKind.Boar || k == AnimalKind.Skunk;
        }

        private static bool IsFlighty(AnimalKind k)
        {
            return !IsPredator(k)
                && !IsDefensive(k)
                && k != AnimalKind.Firefly
                && k != AnimalKind.Starfish
                && k != AnimalKind.Spider;
        }
    }
}
