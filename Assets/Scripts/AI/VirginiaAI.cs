using System;
using UnityEngine;
using UnityEngine.AI;
using Companion.Events;
using Companion.FSM;
using TheForest.Interaction;
using TheForest.Items;
using TheForest.Player;
using TheForest.World;

namespace TheForest.AI
{
    public enum VirginiaState
    {
        Wild,       // Chưa quen: thấy player áp sát/cầm vũ khí là bỏ chạy, không tương tác được
        Curious,    // Đã "quen mặt" quanh base, quan sát/lảng vảng từ xa, sẵn sàng cho Bonding
        Bonding,    // Trong tầm gần, player buông vũ khí & không nhìn thẳng mặt -> tích Trust
        Ally,       // Đã tin tưởng: TỰ theo chân (khác Kelvin — KHÔNG cần lệnh), có thể nhận quà/vũ khí
        Combat,     // Ally + đã được tặng vũ khí + phát hiện địch -> chủ động hỗ trợ (qua VirginiaCombatController)
        Fearful     // Hoảng tạm thời: bị player tấn công / rút vũ khí đột ngột / tiếng động lớn gần
    }

    /// <summary>
    /// AI riêng cho Virginia (Giai đoạn 1 của roadmap) — CỐ TÌNH KHÔNG kế thừa CannibalAI lẫn không
    /// gắn vào Companion.FSM.CompanionFSM (Kelvin), vì hành vi khác biệt hoàn toàn theo GDD thật đã
    /// tra cứu và kiểm chứng qua web search trước khi viết file này:
    ///
    ///   - KHÔNG nhận lệnh (không có CommandIssuedChannelSO, không có Cmd_* nào) — tự quyết định hoàn
    ///     toàn theo Trust, đúng "cannot be commanded like Kelvin, follows automatically once trusted".
    ///   - Làm quen dần: player phải BUÔNG vũ khí (EquipmentController.HasEquipped == false), giữ
    ///     khoảng cách vừa phải, KHÔNG áp sát/sprint, và không ngắm thẳng mặt cô trong lúc gần — khớp
    ///     "holster your weapon, stay calm, avoid direct eye contact, let her approach".
    ///   - Một khi Ally: có thể được TẶNG vũ khí tầm xa qua tương tác E (dual-wield, KHÔNG tốn đạn —
    ///     xem VirginiaCombatController), nhưng cần "CombatConfidence" tích luỹ dần qua các trận sống
    ///     sót mới thực sự dám bắn — khớp "at first still scared of the weapon given, takes a few
    ///     battles before she'll actually use it".
    ///   - Bị chính PLAYER tấn công -> hoảng sợ, tạm ngừng theo/giúp (KHÔNG mất Ally vĩnh viễn trừ khi
    ///     Trust rớt dưới ngưỡng) — khớp "attacking her makes her fearful, she'll stop helping/staying
    ///     around for a while". Không có trạng thái "Virginia thù địch vĩnh viễn" trong game gốc — đã
    ///     xác nhận qua tra cứu, KHÔNG tự thêm cơ chế "Creepy Virginia" suy đoán vào đây.
    ///   - Tái dùng CompanionStatRuntime + CompanionStatDefinitionSO (2 stat: Trust, CombatConfidence)
    ///     đúng khuyến nghị lộ trình Giai đoạn 1 — cần gán 2 asset CompanionStatDefinitionSO trong
    ///     Inspector (vd min 0 / max 100 cho cả hai).
    ///   - Tái dùng FireRegistry (World/FireSource.cs, đã có sẵn) để cô bị thu hút quanh lửa trại đang
    ///     cháy khi Curious/Wild — khớp "attracted to campfires, garden boxes, tents around the base".
    ///   - Máu/gục/hồi/chết vĩnh viễn tách sang VirginiaHealth.cs (component riêng, giống AnimalHealth/
    ///     CannibalHealth) — file này chỉ nhận lại 5 callback (OnAttackedByPlayer/OnAttackedByEnemy/
    ///     OnKnockedDown/OnRevived/OnDeath) để phản ứng về mặt HÀNH VI.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class VirginiaAI : MonoBehaviour, IInteractable
    {
        [Header("Event Channels (Bus)")]
        [SerializeField] private NoiseEventChannelSO noiseChannel;

        [Header("Trust (tái dùng CompanionStatRuntime — gán 1 CompanionStatDefinitionSO, vd 0..100)")]
        [SerializeField] private CompanionStatRuntime trust;
        [SerializeField] private float allyThreshold = 80f;
        [SerializeField] private float trustGainPerSec = 2f;
        [SerializeField] private float trustGainOnGift = 10f;
        [SerializeField] private float trustLossOnWeaponDrawn = 15f;
        [SerializeField] private float trustLossOnPlayerAttack = 40f;

        [Header("Can đảm chiến đấu (stat riêng — GDD: lúc đầu được tặng súng vẫn sợ)")]
        [SerializeField] private CompanionStatRuntime combatConfidence;
        [SerializeField] private float combatReadyThreshold = 50f;
        [SerializeField] private float confidenceGainPerSecondInCombat = 4f;

        [Header("Điều kiện Bonding (player phải buông vũ khí & giữ khoảng cách bình tĩnh)")]
        [SerializeField] private float minBondDistance = 3f;
        [SerializeField] private float maxBondDistance = 9f;

        [Header("Wild (né tránh & làm quen dần theo thời gian)")]
        [SerializeField] private float wildFleeTriggerDistance = 10f;
        [SerializeField] private float wildSightRadius = 16f;
        [Tooltip("Tổng thời gian (giây) player ở trong tầm quan sát mà KHÔNG áp sát/cầm vũ khí, trước khi hết Wild.")]
        [SerializeField] private float familiarityNeededSeconds = 45f;

        [Header("Curious")]
        [SerializeField] private float curiousStopDistance = 10f;

        [Header("Di chuyển & thu hút lửa trại")]
        [SerializeField] private float wanderRadius = 8f;
        [SerializeField] private float wanderSpeed = 2f;
        [SerializeField] private float fleeSpeed = 6f;
        [Tooltip("Bán kính tìm lửa trại đang cháy để đứng gần khi rảnh (FireRegistry đã có sẵn).")]
        [SerializeField] private float fireAttractRadius = 14f;

        [Header("Fearful")]
        [SerializeField] private float fearDurationShort = 3f;
        [SerializeField] private float fearDurationLong = 6f;

        [Header("Theo chân khi Ally")]
        [SerializeField] private float followStopDistance = 3f;
        [SerializeField] private float followSpeed = 4f;
        [SerializeField] private float repathInterval = 0.25f;

        [Header("Quà & vũ khí (FIX fidelity: CHỈ Pistol + Shotgun)")]
        [SerializeField] private VirginiaCombatController combat;
        [SerializeField] private float combatEngageRadius = 16f;
        [SerializeField] private LayerMask enemyMask = ~0;

        [Header("Animator (tuỳ chọn)")]
        [SerializeField] private Animator animator;

        [Header("Tham chiếu")]
        [SerializeField] private Transform player;
        [SerializeField] private EquipmentController playerEquipment;
        [SerializeField] private SurvivalStats playerStats;

        private static readonly int ThumbsUpHash = Animator.StringToHash("ThumbsUp");
        private static readonly int AfraidHash = Animator.StringToHash("Afraid");
        private static readonly int PointAlertHash = Animator.StringToHash("PointAlert");

        // ===================== RUNTIME =====================
        public VirginiaState State { get; private set; } = VirginiaState.Wild;
        public float Trust => trust.Value;
        public float CombatConfidence => combatConfidence.Value;
        public bool HasGpsLocator { get; private set; }

        public event Action<VirginiaState> OnStateChanged;
        public event Action<float> OnTrustChanged;
        public event Action<Transform> OnEnemySpotted;

        private NavMeshAgent _agent;
        private VirginiaHealth _health;
        private Vector3 _home;
        private int _gunsGiven; // số súng (Pistol/Shotgun) đã tặng — dual-wield tối đa 2

        private float _familiarityTimer;
        private float _repathTimer;
        private float _combatTickTimer;
        private Vector3 _fleeFrom;
        private float _fearTimer;

        // ===================== LIFECYCLE =====================
        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<VirginiaHealth>();
            if (combat == null) combat = GetComponent<VirginiaCombatController>();
            _home = transform.position;

            trust.Init();
            combatConfidence.Init();

            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null)
                {
                    player = p.transform;
                    playerEquipment = p.GetComponent<EquipmentController>();
                    playerStats = p.GetComponent<SurvivalStats>();
                }
            }
            VirginiaRegistry.Instance = this;
        }

        private void OnEnable()
        {
            if (noiseChannel != null) noiseChannel.Register(OnNoiseHeard);
        }

        private void OnDisable()
        {
            // CHÚ Ý: chỉ hủy đăng ký noise ở đây. KHÔNG xóa VirginiaRegistry.Instance ở OnDisable —
            // OnKnockedDown() cũng set enabled=false (để khựng Update khi gục), nếu clear registry tại
            // đây thì GPS locator sẽ "mất dấu" cô ngay khi cô vừa gục, dù cô vẫn nằm đó chờ hồi.
            if (noiseChannel != null) noiseChannel.Unregister(OnNoiseHeard);
        }

        private void OnDestroy()
        {
            if (VirginiaRegistry.Instance == this) VirginiaRegistry.Instance = null;
        }

        private void OnNoiseHeard(NoiseEvent e)
        {
            if (State == VirginiaState.Ally || State == VirginiaState.Combat) return; // đã tin tưởng, quen tiếng động

            float radius = wildSightRadius * e.Loudness;
            if ((e.Position - transform.position).sqrMagnitude > radius * radius) return;

            EnterFearful(e.Position, fearDurationShort);
        }

        private void Update()
        {
            switch (State)
            {
                case VirginiaState.Wild: TickWild(); break;
                case VirginiaState.Curious: TickCurious(); break;
                case VirginiaState.Bonding: TickBonding(); break;
                case VirginiaState.Ally: TickAlly(); break;
                case VirginiaState.Combat: TickCombat(); break;
                case VirginiaState.Fearful: TickFearful(); break;
            }
        }

        // ===================== WILD =====================
        private void TickWild()
        {
            if (player == null) { Wander(wanderSpeed); return; }

            float dist = Vector3.Distance(transform.position, player.position);
            bool threatening = dist < wildFleeTriggerDistance || (IsPlayerAggressive() && dist < wildSightRadius);

            if (threatening)
            {
                _familiarityTimer = 0f; // bị doạ -> mất tiến độ làm quen của lượt quan sát này
                FleeFrom(player.position);
                return;
            }

            Wander(wanderSpeed);

            if (dist <= wildSightRadius)
            {
                _familiarityTimer += Time.deltaTime;
                if (_familiarityTimer >= familiarityNeededSeconds)
                    SetState(VirginiaState.Curious);
            }
            else
            {
                _familiarityTimer = Mathf.Max(0f, _familiarityTimer - Time.deltaTime * 0.5f);
            }
        }

        // ===================== CURIOUS =====================
        private void TickCurious()
        {
            if (player == null) { Wander(wanderSpeed); return; }

            float dist = Vector3.Distance(transform.position, player.position);
            bool aggressive = IsPlayerAggressive();

            if (aggressive && dist < curiousStopDistance)
            {
                EnterFearful(player.position, fearDurationShort);
                return;
            }

            if (!aggressive && dist <= maxBondDistance)
            {
                SetState(VirginiaState.Bonding);
                return;
            }

            Wander(wanderSpeed);
        }

        // ===================== BONDING =====================
        private void TickBonding()
        {
            if (player == null) { SetState(VirginiaState.Curious); return; }

            _agent.speed = wanderSpeed;
            float dist = Vector3.Distance(transform.position, player.position);

            if (playerEquipment != null && playerEquipment.HasEquipped)
            {
                trust.Add(-trustLossOnWeaponDrawn);
                OnTrustChanged?.Invoke(trust.Value);
                SetState(VirginiaState.Curious);
                return;
            }

            if (dist > maxBondDistance * 1.5f)
            {
                SetState(VirginiaState.Curious); // player bỏ đi giữa chừng
                return;
            }

            if (IsPlayerCalmAndInBondRange())
            {
                trust.Add(trustGainPerSec * Time.deltaTime);
                OnTrustChanged?.Invoke(trust.Value);
                FaceTarget(player.position);

                if (trust.Value >= allyThreshold)
                    SetState(VirginiaState.Ally);
            }
            else if (!_agent.pathPending && _agent.remainingDistance <= 0.5f)
            {
                // Nhích nhẹ về hướng player thay vì đứng yên, nhưng KHÔNG lao thẳng vào (giữ dè chừng)
                Vector3 mid = Vector3.Lerp(transform.position, player.position, 0.15f);
                if (NavMesh.SamplePosition(mid, out var hit, 3f, NavMesh.AllAreas))
                    _agent.SetDestination(hit.position);
            }
        }

        // ===================== ALLY =====================
        private void TickAlly()
        {
            _agent.speed = followSpeed;
            _repathTimer -= Time.deltaTime;
            if (player != null && _repathTimer <= 0f)
            {
                _repathTimer = repathInterval;
                float d = Vector3.Distance(transform.position, player.position);
                if (d > followStopDistance) _agent.SetDestination(player.position);
                else if (_agent.isOnNavMesh) _agent.ResetPath();
            }

            if (_gunsGiven == 0) return; // chưa có súng -> chỉ theo chân, không tự lao vào đánh

            var hostile = FindNearestHostile(combatEngageRadius);
            if (hostile != null)
            {
                OnEnemySpotted?.Invoke(hostile.transform);
                if (animator != null) animator.SetTrigger(PointAlertHash);
                SetState(VirginiaState.Combat);
            }
        }

        // ===================== COMBAT =====================
        private void TickCombat()
        {
            var hostile = FindNearestHostile(combatEngageRadius * 1.5f); // hysteresis: dễ VÀO Combat hơn RA
            if (hostile == null)
            {
                SetState(VirginiaState.Ally);
                return;
            }

            combat.ReadyToFight = combatConfidence.Value >= combatReadyThreshold;
            combat.SetTarget(hostile.transform);
            combat.Tick();
            FaceTarget(hostile.transform.position);

            _combatTickTimer += Time.deltaTime;
            if (_combatTickTimer >= 1f)
            {
                _combatTickTimer = 0f;
                combatConfidence.Add(confidenceGainPerSecondInCombat); // sống sót qua giao tranh -> dạn dĩ dần
            }
        }

        private CannibalAI FindNearestHostile(float radius)
        {
            var hits = Physics.OverlapSphere(transform.position, radius, enemyMask, QueryTriggerInteraction.Ignore);
            CannibalAI best = null; float bestSqr = radius * radius;
            foreach (var h in hits)
            {
                var ai = h.GetComponentInParent<CannibalAI>();
                if (ai == null || ai.State == CannibalState.Dead) continue;
                float d = (ai.transform.position - transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = ai; }
            }
            return best;
        }

        // ===================== FEARFUL =====================
        private void EnterFearful(Vector3 threat, float duration)
        {
            _fleeFrom = threat;
            _fearTimer = duration;
            SetState(VirginiaState.Fearful);
        }

        private void TickFearful()
        {
            FleeFrom(_fleeFrom);
            _fearTimer -= Time.deltaTime;
            if (_fearTimer <= 0f)
                SetState(trust.Value >= allyThreshold ? VirginiaState.Ally : VirginiaState.Curious);
        }

        // ===================== DI CHUYỂN DÙNG CHUNG =====================
        private void Wander(float speed)
        {
            _agent.speed = speed;
            if (_agent.pathPending || _agent.remainingDistance > 1f) return;

            var fire = FindNearestBurningFire(fireAttractRadius);
            Vector3 dest = fire != null
                ? fire.Position + UnityEngine.Random.insideUnitSphere.normalized * 2.5f
                : _home + UnityEngine.Random.insideUnitSphere * wanderRadius;
            dest.y = transform.position.y;

            if (NavMesh.SamplePosition(dest, out var hit, 4f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        private void FleeFrom(Vector3 threat)
        {
            _agent.speed = fleeSpeed;
            Vector3 away = transform.position - threat; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = UnityEngine.Random.insideUnitSphere;
            Vector3 dest = transform.position + away.normalized * 8f;
            if (NavMesh.SamplePosition(dest, out var hit, 6f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        private FireSource FindNearestBurningFire(float maxDist)
        {
            FireSource best = null; float bestSqr = maxDist * maxDist;
            foreach (var f in FireRegistry.Fires)
            {
                if (f == null || !f.IsBurning) continue;
                float d = (f.Position - transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = f; }
            }
            return best;
        }

        private void FaceTarget(Vector3 pos)
        {
            Vector3 dir = pos - transform.position; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
        }

        private bool IsPlayerAggressive()
        {
            if (playerEquipment != null && playerEquipment.HasEquipped) return true;
            if (playerStats != null && playerStats.IsSprinting) return true;
            return false;
        }

        private bool IsPlayerCalmAndInBondRange()
        {
            // FIX fidelity (#5): BỎ điều kiện "không được nhìn thẳng mặt cô" — đây là cơ chế bịa, KHÔNG có
            // trong game gốc (đã tra cứu: không nguồn nào xác nhận cơ chế ánh nhìn). Trust chỉ cần player
            // BUÔNG vũ khí + giữ khoảng cách bình tĩnh (+ tặng quà/đứng gần lửa xử lý ở nơi khác).
            if (player == null || IsPlayerAggressive()) return false;

            float dist = Vector3.Distance(transform.position, player.position);
            return dist >= minBondDistance && dist <= maxBondDistance;
        }

        private void SetState(VirginiaState next)
        {
            if (State == next) return;
            State = next;
            OnStateChanged?.Invoke(State);

            if (animator != null && next == VirginiaState.Fearful)
                animator.SetTrigger(AfraidHash);
        }

        // ===================== QUÀ & VŨ KHÍ (IInteractable) =====================
        public string GetPrompt()
        {
            if (State == VirginiaState.Wild || State == VirginiaState.Curious) return string.Empty;
            if (_health != null && _health.IsKnockedDown) return "[E] Đỡ Virginia dậy";
            if (playerEquipment != null && playerEquipment.HasEquipped && IsAcceptableGift(playerEquipment.EquippedItem))
                return $"[E] Tặng {playerEquipment.EquippedItem.displayName} cho Virginia";
            return "Virginia";
        }

        public bool CanInteract()
        {
            if (State == VirginiaState.Wild || State == VirginiaState.Curious) return false;
            if (_health != null && _health.IsKnockedDown) return true;
            return playerEquipment != null && playerEquipment.HasEquipped && IsAcceptableGift(playerEquipment.EquippedItem);
        }

        public void Interact(GameObject interactor)
        {
            if (_health != null && _health.IsKnockedDown) { _health.TryRevive(); return; }

            var equipment = interactor.GetComponent<EquipmentController>();
            if (equipment == null || !equipment.HasEquipped) return;
            TryReceiveItem(equipment.EquippedItem, interactor);
        }

        public void OnFocus() { }
        public void OnLoseFocus() { }

        // FIX fidelity (#4): Virginia CHỈ nhận GPS Locator hoặc SÚNG Pistol/Shotgun (FirearmItemData
        // thường, KHÔNG phải Stun Gun) — KHÔNG nhận vũ khí cận chiến/cung như bản cũ. Dual-wield tối đa 2
        // khẩu, không nhận thêm khi đã đủ tay.
        private bool IsAcceptableGift(ItemData item)
        {
            if (item == null) return false;
            if (item is GpsLocatorItemData) return true;
            if (item is FirearmItemData gun && !gun.isStunWeapon)
                return _gunsGiven < 2;
            return false;
        }

        /// <summary>Player equip 1 item hợp lệ rồi nhấn E vào Virginia (Ally/Combat) -> giao nộp.</summary>
        public bool TryReceiveItem(ItemData item, GameObject giver)
        {
            if (!IsAcceptableGift(item)) return false;

            if (item is GpsLocatorItemData)
            {
                HasGpsLocator = true;
            }
            else if (item is FirearmItemData gun)
            {
                _gunsGiven++;
                if (combat != null) combat.SetWeapon(gun);
            }

            var inv = giver != null ? giver.GetComponent<Inventory>() : null;
            if (inv != null && inv.Has(item, 1)) inv.TryConsume(item, 1);
            var equip = giver != null ? giver.GetComponent<EquipmentController>() : null;
            if (equip != null && equip.EquippedItem == item) equip.Unequip();

            trust.Add(trustGainOnGift);
            OnTrustChanged?.Invoke(trust.Value);
            if (animator != null) animator.SetTrigger(ThumbsUpHash);
            return true;
        }

        // ===================== API CHO VirginiaHealth =====================
        public void OnAttackedByPlayer()
        {
            trust.Add(-trustLossOnPlayerAttack);
            OnTrustChanged?.Invoke(trust.Value);
            EnterFearful(player != null ? player.position : transform.position, fearDurationLong);
        }

        public void OnAttackedByEnemy()
        {
            if (State != VirginiaState.Combat)
                EnterFearful(transform.position, fearDurationShort);
        }

        public void OnKnockedDown()
        {
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
            enabled = false; // khựng Update — VirginiaHealth điều khiển hồi/hết giờ
        }

        public void OnRevived()
        {
            enabled = true;
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = false;
            SetState(VirginiaState.Ally); // hồi dậy quay lại theo chân, KHÔNG tự nhảy thẳng vào Combat
        }

        public void OnDeath()
        {
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
            enabled = false; // VĨNH VIỄN — VirginiaHealth.IsDead đã true, hệ Save/UI ngoài tự xử lý phần còn lại
        }
    }

    /// <summary>Registry tối giản (giống FireRegistry/CorpseRegistry) để HUD/la bàn hỏi vị trí Virginia cho GPS Locator.</summary>
    public static class VirginiaRegistry
    {
        public static VirginiaAI Instance { get; set; }

        /// <summary>
        /// FIX (#low — GPS Locator): vị trí Virginia cho la bàn/bản đồ vẽ chấm định vị. Trả null nếu chưa
        /// tặng GPS Locator (HasGpsLocator=false) hoặc chưa có Virginia trong scene. Trước đây HasGpsLocator
        /// được set nhưng KHÔNG có API nào lấy được vị trí -> tính năng chết; nay hoạt động thật.
        /// </summary>
        public static Vector3? TrackedPosition =>
            Instance != null && Instance.HasGpsLocator ? Instance.transform.position : (Vector3?)null;
    }
}
