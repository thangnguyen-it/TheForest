using UnityEngine;
using UnityEngine.AI;
using TheForest.Player;
using TheForest.World;
using Companion.Events;

namespace TheForest.AI
{
    public enum CannibalState { Patrol, Investigate, Chase, Attack, Stunned, Knockdown, Fear, Worship, Mourn, Dead }

    [RequireComponent(typeof(NavMeshAgent))]
    public class CannibalAI : MonoBehaviour
    {
        [Header("Event Channels (Bus)")]
        [SerializeField] private NoiseEventChannelSO noiseChannel;
        [SerializeField] private AggressionEventChannelSO aggressionChannel;

        [Header("Mục tiêu")]
        [SerializeField] private Transform player;
        [SerializeField] private SurvivalStats playerStats;

        [Header("Tuần tra (Patrol)")]
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private float patrolSpeed = 2f;
        [SerializeField] private float waypointTolerance = 1.2f;
        [SerializeField] private float idleAtWaypoint = 2f;

        [Header("Phát hiện - Thị giác (Nón nhìn)")]
        [SerializeField] private float viewRadius = 18f;
        [SerializeField] private float viewAngle = 100f;
        [SerializeField] private Transform eye;
        [SerializeField] private LayerMask losObstacles = ~0;

        [Header("Phát hiện - Thính giác (Nghe)")]
        [SerializeField] private float hearingRadius = 14f;

        [Header("Truy đuổi & Tấn công")]
        [SerializeField] private float chaseSpeed = 4.5f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float attackDamage = 12f;
        [SerializeField] private float attackCooldown = 1.5f;
        [SerializeField] private float loseSightTime = 5f;

        [Header("Hệ thống Aggression")]
        [SerializeField] private int zoneId = 0;
        [SerializeField] private bool ignoreAggressionSystem = false;
        [SerializeField] private float aggressionDetectionMult = 1f;

        [Header("Sợ lửa / effigy")]
        [SerializeField] private bool fearsFire = true;
        [SerializeField] private float fireFleeDistance = 10f;

        // FIX fidelity (#2): đã GỠ nhóm field "Phản ứng Sơn đỏ / Worship" (affectedByWarpaint,
        // ignoreThreshold, worshipChance, worshipDuration) — cơ chế The Forest, không có trong SotF.

        [Header("Mourn (Khóc thương đồng loại)")]
        [SerializeField] private bool canMourn = false;
        [SerializeField] private float mournSightRange = 12f;
        [SerializeField] private float mournDuration = 3f;
        [Range(0f, 1f)][SerializeField] private float mournChance = 0.6f;

        [Header("Đồng bộ chuyển động")]
        [SerializeField] private Animator locomotionAnimator;

        [Header("Nhóm & vai trò")]
        [SerializeField] private CannibalType role = CannibalType.Male;
        [SerializeField] private bool isLeader = false;
        [SerializeField] private float surroundRadius = 4f;
        [SerializeField] private float reviveRange = 6f;
        [SerializeField] private float reviveDuration = 2f;

        [Header("Kéo xác & chôn (Thường)")]
        [SerializeField] private bool canCarryCorpse = true;
        [SerializeField] private float corpseSearchRange = 14f;
        [SerializeField] private float burySafeDistance = 18f;
        [SerializeField] private float buryDuration = 3f;
        [SerializeField] private Transform carryAnchor;

        [Header("Ăn xác (Starving)")]
        [SerializeField] private bool eatsCorpses = false;
        [SerializeField] private float corpseSmellRange = 16f;
        [SerializeField] private float biteInterval = 0.8f;
        [SerializeField] private float eatInterruptDist = 2.5f;

        [Header("Trèo cây quan sát (Scout)")]
        [SerializeField] private bool canClimbTrees = false;
        [Range(0f, 1f)][SerializeField] private float climbChance = 0.3f;
        [SerializeField] private float climbSearchRange = 20f;
        [SerializeField] private float climbSpeed = 3f;
        [SerializeField] private float perchObserveTime = 8f;
        [SerializeField] private float perchViewBonus = 1.6f;

        // ===================== RUNTIME =====================
        public CannibalState State { get; private set; } = CannibalState.Patrol;
        public int ZoneId => zoneId;
        public bool IsUnaware => State == CannibalState.Patrol || State == CannibalState.Investigate;
        public Vector3 Forward => transform.forward;

        private NavMeshAgent _agent;
        private CannibalAttack _attack;
        private TheForest.Player.PlayerMudCamo _playerStealth; // FIX #2: crouch-stealth SotF thật
        private CannibalGroup _group;
        private CannibalHealth _health;

        private int _patrolIndex;
        private float _idleTimer;
        private float _attackTimer;
        private float _lostSightTimer;
        private float _stateLockTimer;
        private Vector3 _investigatePoint;
        private bool _hasInvestigateTarget;

        private bool _isReviving;
        private bool _isMourning;

        private bool _isCarrying;
        private CannibalCorpse _carried;

        private bool _isEating;
        private CannibalCorpse _eatingCorpse;

        private bool _isClimbing;
        private bool _onPerch;
        private TreeClimbSpot _climbSpot;
        private float _climbCheckTimer;

        // Coroutine Handles
        private Coroutine _carryCo;
        private Coroutine _eatCo;
        private Coroutine _climbCo;

        // ===================== HELPER =====================
        private void SetState(CannibalState s) => State = s;
        private static readonly int SpeedParam = Animator.StringToHash("Speed");

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _attack = GetComponent<CannibalAttack>();
            _health = GetComponent<CannibalHealth>();

            if (eye == null) eye = transform;
            if (locomotionAnimator == null) locomotionAnimator = GetComponentInChildren<Animator>();

            if (player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null)
                {
                    player = p.transform;
                    playerStats = p.GetComponent<SurvivalStats>();
                    _playerStealth = p.GetComponent<TheForest.Player.PlayerMudCamo>();
                }
            }
            else
            {
                _playerStealth = player.GetComponent<TheForest.Player.PlayerMudCamo>();
            }

            if (_attack != null) attackRange = _attack.AttackRange;
        }

        private void OnEnable()
        {
            if (noiseChannel != null) noiseChannel.Register(OnNoiseHeard);
        }

        private void OnDisable()
        {
            if (noiseChannel != null) noiseChannel.Unregister(OnNoiseHeard);
        }

        // Adapter: bus -> logic nghe cũ. Người nghe tự quyết có nghe được không.
        private void OnNoiseHeard(NoiseEvent e)
        {
            HearNoise(e.Position, e.Loudness);
        }

        public void SetGroup(CannibalGroup g) => _group = g;

        private void Update()
        {
            if (State == CannibalState.Dead)
            {
                if (locomotionAnimator != null) locomotionAnimator.SetFloat(SpeedParam, 0f);
                return;
            }

            // Bảo vệ State đặc biệt
            if (_isMourning || State == CannibalState.Worship)
            {
                if (locomotionAnimator != null) locomotionAnimator.SetFloat(SpeedParam, 0f);
                return;
            }

            // Khóa State khi Trèo Cây (Mọi logic agent tắt, để Coroutine lo)
            if (_isClimbing) return;

            // Khóa State khi dính khống chế
            if (State == CannibalState.Stunned || State == CannibalState.Knockdown)
            {
                _stateLockTimer -= Time.deltaTime;
                if (locomotionAnimator != null) locomotionAnimator.SetFloat(SpeedParam, 0f);
                if (_stateLockTimer <= 0f) SetState(CannibalState.Chase);
                return;
            }

            // Sợ lửa
            if (fearsFire && State != CannibalState.Knockdown && State != CannibalState.Stunned)
            {
                var fire = TheForest.World.FireRegistry.GetStrongestFearAt(transform.position, out float fearStr);
                if (fire != null && fearStr > 0.05f)
                {
                    EnterFear(fire.Position);
                    TickFear(fire.Position);

                    aggressionChannel?.Raise(new AggressionDelta(zoneId, -fearStr * 0.5f * Time.deltaTime));

                    if (locomotionAnimator != null) locomotionAnimator.SetFloat(SpeedParam, _agent.velocity.magnitude);
                    return;
                }
                else if (State == CannibalState.Fear)
                {
                    SetState(player != null && CanSeePlayer() ? CannibalState.Chase : CannibalState.Patrol);
                }
            }

            // FIX fidelity (#2): ĐÃ GỠ cơ chế "sơn đỏ khiến cannibal quỳ lạy/bỏ qua" — đó là mechanic của
            // The Forest (2014), KHÔNG có trong Sons of the Forest. Tàng hình SotF xử lý bằng crouch trong
            // CanSeePlayer(). PlayerWarpaint giữ lại chỉ như hiệu ứng hình ảnh (cosmetic), không ảnh hưởng AI.

            // ƯU TIÊN 1: Ăn Xác (Starving) - Ăn cả giữa trận
            if (eatsCorpses && !_isEating && State != CannibalState.Attack)
            {
                var corpse = CorpseRegistry.FindUnclaimedNear(transform.position, corpseSmellRange);
                if (corpse != null)
                {
                    bool playerInFace = player != null && DistanceToPlayer() < eatInterruptDist && CanSeePlayer();
                    if (!playerInFace && corpse.TryClaim())
                    {
                        if (_eatCo != null) StopCoroutine(_eatCo);
                        _eatCo = StartCoroutine(EatCorpse(corpse));
                        return;
                    }
                }
            }

            // ƯU TIÊN 2: Kéo Xác (Thường)
            if (canCarryCorpse && !_isCarrying && !_isReviving && State != CannibalState.Attack)
            {
                bool playerPressing = player != null && DistanceToPlayer() < attackRange * 2f && CanSeePlayer();
                if (!playerPressing)
                {
                    var corpse = CorpseRegistry.FindUnclaimedNear(transform.position, corpseSearchRange);
                    if (corpse != null && corpse.TryClaim())
                    {
                        if (_carryCo != null) StopCoroutine(_carryCo);
                        _carryCo = StartCoroutine(CarryAndBury(corpse));
                        return;
                    }
                }
            }

            // ƯU TIÊN 3: Đỡ đồng đội
            if (!_isReviving && !_isCarrying && !_isEating && _group != null && State != CannibalState.Attack && State != CannibalState.Fear)
            {
                var downed = _group.FindDownedAllyNear(transform.position, reviveRange);
                if (downed != null && DistanceToPlayer() > attackRange * 1.5f)
                {
                    StartCoroutine(ReviveAlly(downed));
                    return;
                }
            }

            aggressionDetectionMult = CurrentAggression();
            bool canSee = CanSeePlayer();

            switch (State)
            {
                case CannibalState.Patrol: TickPatrol(canSee); break;
                case CannibalState.Investigate: TickInvestigate(canSee); break;
                case CannibalState.Chase: TickChase(canSee); break;
                case CannibalState.Attack: TickAttack(canSee); break;
            }

            if (_attackTimer > 0f) _attackTimer -= Time.deltaTime;
            if (locomotionAnimator != null) locomotionAnimator.SetFloat(SpeedParam, _agent.velocity.magnitude);
        }

        // ===================== INTERRUPTIONS (Bảo vệ Coroutines) =====================
        private void InterruptActions()
        {
            if (_isCarrying && _carried != null)
            {
                if (_carryCo != null) StopCoroutine(_carryCo);
                _carried.Detach();
                _carried.Release();
                _isCarrying = false;
                _carried = null;
            }
            if (_isEating && _eatingCorpse != null)
            {
                if (_eatCo != null) StopCoroutine(_eatCo);
                _eatingCorpse.Release();
                _isEating = false;
                _eatingCorpse = null;
            }
            if (_isClimbing)
            {
                ForceFallOff();
            }
        }

        // ===================== EAT CORPSE (STARVING) =====================
        private System.Collections.IEnumerator EatCorpse(CannibalCorpse corpse)
        {
            _isEating = true;
            _eatingCorpse = corpse;
            SetState(CannibalState.Investigate);

            _agent.speed = chaseSpeed;
            while (corpse != null && !corpse.IsEaten)
            {
                _agent.SetDestination(corpse.transform.position);
                if (Vector3.Distance(transform.position, corpse.transform.position) < 1.5f) break;
                if (PlayerInFace()) { AbortEat(corpse); yield break; }
                yield return null;
            }

            if (corpse == null) { _isEating = false; _eatingCorpse = null; SetState(CannibalState.Patrol); yield break; }
            if (_agent.isOnNavMesh) _agent.ResetPath();

            while (corpse != null && !corpse.IsEaten)
            {
                if (PlayerInFace()) { AbortEat(corpse); yield break; }

                if (locomotionAnimator != null) locomotionAnimator.SetTrigger("Eat");

                float gained = corpse.EatBite();
                if (_health != null) _health.HealAmount(gained);

                yield return new WaitForSeconds(biteInterval);
            }

            _isEating = false;
            _eatingCorpse = null;
            SetState(player != null && CanSeePlayer() ? CannibalState.Chase : CannibalState.Patrol);
        }

        private bool PlayerInFace() => player != null && DistanceToPlayer() < eatInterruptDist && CanSeePlayer();

        private void AbortEat(CannibalCorpse corpse)
        {
            corpse?.Release();
            _isEating = false;
            _eatingCorpse = null;
            SetState(CannibalState.Chase);
        }

        // ===================== TREE CLIMBING (SCOUT) =====================
        private System.Collections.IEnumerator ClimbRoutine(TreeClimbSpot spot)
        {
            _isClimbing = true;
            _climbSpot = spot;

            _agent.speed = patrolSpeed * 1.2f;
            while (Vector3.Distance(transform.position, spot.BasePosition) > waypointTolerance)
            {
                _agent.SetDestination(spot.BasePosition);
                if (CanSeePlayer()) { EndClimb(); SetState(CannibalState.Chase); yield break; }
                yield return null;
            }

            _agent.enabled = false;
            if (locomotionAnimator != null) locomotionAnimator.SetTrigger("Climb");
            yield return MoveTo(transform.position, spot.PerchPosition, climbSpeed);
            _onPerch = true;

            float baseRadius = viewRadius;
            viewRadius *= perchViewBonus;
            if (locomotionAnimator != null) locomotionAnimator.SetTrigger("Observe");

            float t = perchObserveTime;
            bool spotted = false;
            while (t > 0f)
            {
                FaceTarget(player != null ? player.position : transform.position + transform.forward);
                if (CanSeePlayer()) { spotted = true; break; }
                t -= Time.deltaTime;
                yield return null;
            }
            viewRadius = baseRadius;

            if (locomotionAnimator != null) locomotionAnimator.SetTrigger(spotted ? "JumpDown" : "ClimbDown");
            yield return MoveTo(transform.position, spot.BasePosition, climbSpeed * (spotted ? 2.5f : 1f));

            _onPerch = false;
            _agent.enabled = true;
            if (_agent.isOnNavMesh) _agent.Warp(spot.BasePosition);

            EndClimb();
            SetState(spotted ? CannibalState.Chase : CannibalState.Patrol);
        }

        private System.Collections.IEnumerator MoveTo(Vector3 from, Vector3 to, float speed)
        {
            float dist = Vector3.Distance(from, to);
            float dur = Mathf.Max(0.1f, dist / Mathf.Max(0.1f, speed));
            float e = 0f;
            while (e < dur)
            {
                e += Time.deltaTime;
                transform.position = Vector3.Lerp(from, to, e / dur);
                yield return null;
            }
            transform.position = to;
        }

        private void EndClimb()
        {
            _isClimbing = false;
            _onPerch = false;
            if (_climbSpot != null) { _climbSpot.Vacate(); _climbSpot = null; }
            if (!_agent.enabled) _agent.enabled = true;
        }

        private void ForceFallOff()
        {
            if (_climbCo != null) StopCoroutine(_climbCo);
            if (_climbSpot != null)
            {
                if (!_agent.enabled) _agent.enabled = true;
                if (_agent.isOnNavMesh) _agent.Warp(_climbSpot.BasePosition);
                _climbSpot.Vacate();
                _climbSpot = null;
            }
            _isClimbing = false;
            _onPerch = false;
        }

        // ===================== CARRY & BURY (THƯỜNG) =====================
        private System.Collections.IEnumerator CarryAndBury(CannibalCorpse corpse)
        {
            _isCarrying = true;
            _carried = corpse;
            SetState(CannibalState.Investigate);

            _agent.speed = chaseSpeed;
            while (corpse != null && !corpse.IsBuried)
            {
                _agent.SetDestination(corpse.transform.position);
                if (Vector3.Distance(transform.position, corpse.transform.position) < 1.5f) break;
                if (PlayerInterruptsCarry()) { AbortCarry(corpse); yield break; }
                yield return null;
            }

            if (corpse == null) { _isCarrying = false; _carried = null; yield break; }

            corpse.AttachTo(carryAnchor != null ? carryAnchor : transform);
            Vector3 safeSpot = FindSafeBurySpot();

            while (corpse != null)
            {
                _agent.SetDestination(safeSpot);
                if (!_agent.pathPending && _agent.remainingDistance <= waypointTolerance) break;
                if (PlayerInterruptsCarry()) { corpse.Detach(); AbortCarry(corpse); yield break; }
                yield return null;
            }

            if (_agent.isOnNavMesh) _agent.ResetPath();
            if (locomotionAnimator != null) locomotionAnimator.SetTrigger("Bury");
            yield return new WaitForSeconds(buryDuration);

            corpse?.Detach();
            corpse?.Bury();

            _isCarrying = false;
            _carried = null;
            SetState(CannibalState.Patrol);
        }

        private bool PlayerInterruptsCarry() => player != null && DistanceToPlayer() < attackRange * 1.5f && CanSeePlayer();

        private void AbortCarry(CannibalCorpse corpse)
        {
            corpse?.Release();
            _isCarrying = false;
            _carried = null;
            SetState(CannibalState.Chase);
        }

        private Vector3 FindSafeBurySpot()
        {
            Vector3 away = player != null ? (transform.position - player.position) : transform.forward;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;

            Vector3 target = transform.position + away.normalized * burySafeDistance;
            if (UnityEngine.AI.NavMesh.SamplePosition(target, out var hit, 6f, UnityEngine.AI.NavMesh.AllAreas))
                return hit.position;
            return transform.position;
        }

        // ===================== FEAR & WORSHIP & MOURN =====================
        private void EnterFear(Vector3 firePos) { if (State != CannibalState.Fear) SetState(CannibalState.Fear); }

        private void TickFear(Vector3 firePos)
        {
            _agent.speed = chaseSpeed;
            Vector3 away = (transform.position - firePos); away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            Vector3 target = firePos + away.normalized * fireFleeDistance;

            if (UnityEngine.AI.NavMesh.SamplePosition(target, out var hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        public void NotifyAllyDied(Vector3 deathPos)
        {
            if (_group != null)
                foreach (var m in _group.Members)
                    if (m != null && m != this) m.ReactToAllyDeath(deathPos);
        }

        public void ReactToAllyDeath(Vector3 deathPos)
        {
            if (!canMourn || _isMourning) return;
            if (State == CannibalState.Dead || State == CannibalState.Knockdown || State == CannibalState.Stunned || State == CannibalState.Worship || State == CannibalState.Fear) return;
            if (Vector3.Distance(transform.position, deathPos) > mournSightRange) return;
            if (Random.value > mournChance) return;

            StartCoroutine(MournRoutine(deathPos));
        }

        private System.Collections.IEnumerator MournRoutine(Vector3 deathPos)
        {
            _isMourning = true;
            SetState(CannibalState.Mourn);
            if (_agent.isOnNavMesh) _agent.ResetPath();
            _agent.speed = 0f;

            FaceTarget(deathPos);
            if (locomotionAnimator != null) locomotionAnimator.SetTrigger("Mourn");

            yield return new WaitForSeconds(mournDuration);

            _isMourning = false;
            SetState(player != null && CanSeePlayer() ? CannibalState.Chase : CannibalState.Patrol);
        }

        // ===================== CORE LOGIC =====================
        private void TickPatrol(bool canSee)
        {
            _agent.speed = patrolSpeed;
            if (canSee)
            {
                EnterChaseAlert();
                SetState(CannibalState.Chase);
                return;
            }

            // Scout Tree Climb
            if (canClimbTrees && !_isClimbing)
            {
                _climbCheckTimer -= Time.deltaTime;
                if (_climbCheckTimer <= 0f)
                {
                    _climbCheckTimer = 4f;
                    if (Random.value < climbChance)
                    {
                        var spot = ClimbSpotRegistry.FindFreeNear(transform.position, climbSearchRange);
                        if (spot != null && spot.TryOccupy())
                        {
                            if (_climbCo != null) StopCoroutine(_climbCo);
                            _climbCo = StartCoroutine(ClimbRoutine(spot));
                            return;
                        }
                    }
                }
            }

            if (patrolPoints == null || patrolPoints.Length == 0) return;

            if (!_agent.pathPending && _agent.remainingDistance <= waypointTolerance)
            {
                _idleTimer -= Time.deltaTime;
                if (_idleTimer <= 0f)
                {
                    _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
                    _agent.SetDestination(patrolPoints[_patrolIndex].position);
                    _idleTimer = idleAtWaypoint;
                }
            }
            else if (_agent.destination == Vector3.zero || !_agent.hasPath)
            {
                _agent.SetDestination(patrolPoints[_patrolIndex].position);
            }
        }

        private void TickInvestigate(bool canSee)
        {
            _agent.speed = patrolSpeed * 1.3f;
            if (canSee) { EnterChaseAlert(); SetState(CannibalState.Chase); return; }

            if (_hasInvestigateTarget)
            {
                _agent.SetDestination(_investigatePoint);
                if (!_agent.pathPending && _agent.remainingDistance <= waypointTolerance)
                {
                    _hasInvestigateTarget = false;
                    _idleTimer = idleAtWaypoint;
                }
            }
            else
            {
                _idleTimer -= Time.deltaTime;
                if (_idleTimer <= 0f) SetState(CannibalState.Patrol);
            }
        }

        private void TickChase(bool canSee)
        {
            _agent.speed = chaseSpeed * Mathf.Lerp(1f, 1.25f, Mathf.InverseLerp(1f, 3f, CurrentAggression()));
            bool ranged = _attack != null && _attack.IsRanged;

            if (canSee)
            {
                _lostSightTimer = loseSightTime;

                if (ranged)
                {
                    float ideal = _attack.RangedRange * 0.7f;
                    float d = DistanceToPlayer();

                    if (d < ideal * 0.6f)
                    {
                        Vector3 away = (transform.position - player.position); away.y = 0f;
                        Vector3 dest = transform.position + away.normalized * 4f;
                        if (UnityEngine.AI.NavMesh.SamplePosition(dest, out var h, 3f, UnityEngine.AI.NavMesh.AllAreas))
                            _agent.SetDestination(h.position);
                    }
                    else if (d > _attack.RangedRange) _agent.SetDestination(player.position);
                    else
                    {
                        if (_agent.isOnNavMesh) _agent.ResetPath();
                        SetState(CannibalState.Attack);
                        return;
                    }
                }
                else
                {
                    if (_group != null && DistanceToPlayer() > attackRange * 1.2f)
                        _agent.SetDestination(_group.GetSurroundPosition(this, player, surroundRadius));
                    else
                        _agent.SetDestination(player.position);

                    if (DistanceToPlayer() <= attackRange) { SetState(CannibalState.Attack); return; }
                }
            }
            else
            {
                _lostSightTimer -= Time.deltaTime;
                _agent.SetDestination(player.position);
                if (_lostSightTimer <= 0f)
                {
                    _investigatePoint = player.position;
                    _hasInvestigateTarget = true;
                    SetState(CannibalState.Investigate);
                }
            }
        }

        private void TickAttack(bool canSee)
        {
            bool ranged = _attack != null && _attack.IsRanged;
            float effRange = ranged ? _attack.RangedRange : attackRange;

            _agent.speed = 0f;
            if (_agent.isOnNavMesh) _agent.ResetPath();
            FaceTarget(player.position);

            if (DistanceToPlayer() > effRange && (_attack == null || !_attack.IsAttacking))
            {
                SetState(CannibalState.Chase);
                return;
            }

            if (_attack != null) _attack.TryAttack(player, playerStats);
            else
            {
                if (_attackTimer <= 0f)
                {
                    _attackTimer = attackCooldown / Mathf.Clamp(CurrentAggression(), 1f, 3f);
                    if (playerStats != null) playerStats.ApplyDamage(attackDamage);
                }
            }
        }

        private bool CanSeePlayer()
        {
            if (player == null) return false;

            // FIX fidelity (#2): tàng hình SotF = NGỒI RÓN (crouch), KHÔNG phải sơn đỏ/bùn. Player rón thu
            // nhỏ tầm phát hiện; rón sát (stealth cao) trong tầm gần thì coi như chưa bị thấy.
            float stealth = _playerStealth != null ? _playerStealth.TotalStealth : 0f;

            float radius = viewRadius * aggressionDetectionMult * (1f - stealth * 0.7f);
            Vector3 toPlayer = player.position - eye.position;
            float dist = toPlayer.magnitude;

            if (dist > radius) return false;
            if (Vector3.Angle(transform.forward, toPlayer.normalized) > viewAngle * 0.5f) return false;

            if (Physics.Raycast(eye.position, toPlayer.normalized, out RaycastHit hit, dist, losObstacles))
                if (!hit.collider.CompareTag("Player")) return false;

            return true;
        }

        // ===================== API ĐỒNG BỘ NGOÀI =====================
        private void EnterChaseAlert() { if (_group != null) _group.AlertGroup(player, this); }

        public void OnGroupAlert(Transform p)
        {
            if (State == CannibalState.Dead) return;
            if (player == null) player = p;
            _lostSightTimer = loseSightTime;
            if (State == CannibalState.Patrol || State == CannibalState.Investigate)
                SetState(CannibalState.Chase);
        }

        private System.Collections.IEnumerator ReviveAlly(CannibalAI ally)
        {
            _isReviving = true;
            _agent.speed = chaseSpeed;
            while (ally != null && ally.State == CannibalState.Knockdown)
            {
                _agent.SetDestination(ally.transform.position);
                if (Vector3.Distance(transform.position, ally.transform.position) < 1.5f)
                {
                    if (_agent.isOnNavMesh) _agent.ResetPath();
                    yield return new WaitForSeconds(reviveDuration);
                    ally?.ForceRecover();
                    break;
                }
                if (DistanceToPlayer() <= attackRange * 1.5f) break;
                yield return null;
            }
            _isReviving = false;
        }

        public void ForceRecover()
        {
            if (State == CannibalState.Knockdown || State == CannibalState.Stunned)
            {
                _stateLockTimer = 0f;
                SetState(CannibalState.Chase);
            }
        }

        public void HearNoise(Vector3 position, float loudness)
        {
            if (State == CannibalState.Dead || State == CannibalState.Chase || State == CannibalState.Attack || State == CannibalState.Fear) return;

            float radius = hearingRadius * loudness;
            if ((position - transform.position).sqrMagnitude > radius * radius) return;

            _investigatePoint = position;
            _hasInvestigateTarget = true;
            SetState(CannibalState.Investigate);

            if (isLeader && _group != null && player != null) _group.AlertGroup(player, this);
        }

        public void OnAttacked(Transform attacker)
        {
            if (State == CannibalState.Dead) return;
            InterruptActions();
            _lostSightTimer = loseSightTime;
            if (State != CannibalState.Knockdown && State != CannibalState.Stunned && State != CannibalState.Fear && State != CannibalState.Worship)
                SetState(CannibalState.Chase);
        }

        public void Stun(float duration)
        {
            if (State == CannibalState.Dead) return;
            InterruptActions();
            _attack?.CancelAttack();
            _stateLockTimer = duration;
            _agent.ResetPath();
            SetState(CannibalState.Stunned);
        }

        public void Knockdown(float duration)
        {
            if (State == CannibalState.Dead) return;
            InterruptActions();
            _attack?.CancelAttack();
            _stateLockTimer = duration;
            _agent.ResetPath();
            SetState(CannibalState.Knockdown);
        }

        public void OnDeath()
        {
            InterruptActions();
            _group?.RemoveMember(this);
            SetState(CannibalState.Dead);
            _attack?.CancelAttack();
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
            enabled = false;
        }

        public void SetAggressionMultiplier(float mult) => aggressionDetectionMult = Mathf.Max(0.1f, mult);

        public void ConfigureFromSpawn(int zone, Transform[] patrol, CannibalConfig cfg)
        {
            zoneId = zone;
            if (patrol != null && patrol.Length > 0) patrolPoints = patrol;

            if (cfg != null)
            {
                role = cfg.type;
                ignoreAggressionSystem = cfg.isCreepyMutant;
                fearsFire = !cfg.isCreepyMutant;
                canMourn = (cfg.type == CannibalType.Female);

                // Phân cực hành vi giữa Starving và Cannibal thường / Scout
                eatsCorpses = (cfg.tribe == CannibalTribe.Starving);
                canCarryCorpse = (cfg.tribe != CannibalTribe.Starving);
                canClimbTrees = (cfg.type == CannibalType.Skinny);

                chaseSpeed *= cfg.speedMultiplier;
                patrolSpeed *= cfg.speedMultiplier;
                attackDamage *= cfg.damageMultiplier;

                if (_health != null) _health.ScaleMaxHealth(cfg.healthMultiplier);
                if (_attack != null) _attack.SetCreepy(cfg.isCreepyMutant);
            }

            if (patrolPoints != null && patrolPoints.Length > 0 && _agent != null && _agent.isOnNavMesh)
                _agent.SetDestination(patrolPoints[0].position);
        }

        private float CurrentAggression()
        {
            if (ignoreAggressionSystem || AggressionManager.Instance == null) return 1f;
            return AggressionManager.Instance.GetEffectiveAggression(zoneId);
        }

        private float DistanceToPlayer() => player == null ? 999f : Vector3.Distance(transform.position, player.position);

        private void FaceTarget(Vector3 pos)
        {
            Vector3 dir = (pos - transform.position); dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
        }
    }
}