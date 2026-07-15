using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TheForest.Interaction;
using TheForest.Items;

namespace TheForest.AI
{
    /// <summary>
    /// Chiến đấu tự động của Virginia khi VirginiaAI chuyển sang VirginiaState.Combat.
    ///
    /// FIX fidelity (#4 — đã kiểm chứng qua tra cứu wiki chính thức):
    ///   - Virginia CHỈ dùng SÚNG TẦM XA và ĐÚNG 2 loại: PISTOL + SHOTGUN (FirearmItemData thường,
    ///     KHÔNG phải Stun Gun). Bản cũ dùng Bow+Arrow tạm và có nhánh Axe cận chiến — ĐÃ BỎ.
    ///   - Cô có BA TAY nên cầm được CẢ HAI khẩu cùng lúc (dual-wield) — theo dõi bằng danh sách _guns
    ///     (tối đa 2). Mỗi phát luân phiên giữa các khẩu đang có.
    ///   - KHÔNG tốn đạn: đọc damage theo caliber từ ammoDatabase nhưng KHÔNG hề tiêu Inventory (cô
    ///     không có Inventory). Đúng "does not consume ammo".
    ///   - Bắn HITSCAN y như FirearmController của player (súng thật trong game là hitscan, không phải
    ///     đầu đạn bay) — raycast từ muzzle tới mục tiêu, áp damage IChoppable -> IDamageable.
    ///
    /// KHÔNG tự tìm mục tiêu — VirginiaAI quét địch (CannibalAI.State) và truyền vào qua SetTarget().
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class VirginiaCombatController : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private Animator animator;
        [Tooltip("Kéo các AmmoItemData vào đây để tra damage theo caliber súng được tặng. KHÔNG tiêu đạn.")]
        [SerializeField] private AmmoItemData[] ammoDatabase;
        [SerializeField] private bool loadAmmoFromResources = true;
        [SerializeField] private string ammoResourcePath = "SotFData/Items";
        [Tooltip("Layer địch/vật cản mà phát bắn của Virginia có thể trúng.")]
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Tầm xa (khớp Pistol/Shotgun thật)")]
        [SerializeField] private float preferredRange = 12f;
        [SerializeField] private float minKiteRange = 6f;
        [SerializeField] private float fireCooldown = 1.1f;
        [SerializeField] private float hitscanRange = 60f;

        [Header("Chưa dạn (được tặng súng nhưng còn sợ — cần vài trận mới dám dùng)")]
        [SerializeField] private float skittishKiteRange = 14f;

        private static readonly int FireHash = Animator.StringToHash("Fire");

        private NavMeshAgent _agent;
        private readonly List<FirearmItemData> _guns = new List<FirearmItemData>(2);
        private int _nextGun;
        private Transform _target;
        private float _cooldownTimer;

        /// <summary>VirginiaAI set theo CombatConfidence hiện tại — chưa đủ thì chỉ né, KHÔNG bắn.</summary>
        public bool ReadyToFight { get; set; }
        public bool HasWeapon => _guns.Count > 0;
        public int GunCount => _guns.Count;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            LoadResourceAmmo();
        }

        private void LoadResourceAmmo()
        {
            if (!loadAmmoFromResources) return;

            var resourceAmmo = Resources.LoadAll<AmmoItemData>(ammoResourcePath);
            if (resourceAmmo == null || resourceAmmo.Length == 0) return;

            var merged = new List<AmmoItemData>();
            if (ammoDatabase != null)
            {
                foreach (var ammo in ammoDatabase)
                    if (ammo != null && !merged.Contains(ammo)) merged.Add(ammo);
            }

            foreach (var ammo in resourceAmmo)
                if (ammo != null && !merged.Contains(ammo)) merged.Add(ammo);

            ammoDatabase = merged.ToArray();
        }

        /// <summary>Player tặng 1 khẩu Pistol/Shotgun. Dual-wield tối đa 2 khẩu; trùng loại thì bỏ qua.</summary>
        public void SetWeapon(FirearmItemData gun)
        {
            if (gun == null || gun.isStunWeapon) return;      // Virginia KHÔNG dùng Stun Gun
            if (_guns.Contains(gun)) return;
            if (_guns.Count >= 2) _guns[1] = gun;             // thay khẩu thứ 2 nếu đã đầy tay
            else _guns.Add(gun);
        }

        public void SetTarget(Transform target) => _target = target;

        /// <summary>Gọi mỗi frame từ VirginiaAI.TickCombat() sau khi đã SetTarget.</summary>
        public void Tick()
        {
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;
            if (_target == null || _guns.Count == 0) return;

            float dist = Vector3.Distance(transform.position, _target.position);

            if (!ReadyToFight)
            {
                // Chưa đủ can đảm: giữ khoảng cách an toàn, TUYỆT ĐỐI không bắn (đúng hành vi thật lúc
                // mới được tặng súng — cô đi theo nhưng chưa dám dùng).
                Reposition(skittishKiteRange);
                return;
            }

            if (dist < minKiteRange) Reposition(preferredRange);
            else if (dist > preferredRange) _agent.SetDestination(_target.position);
            else if (_agent.isOnNavMesh) _agent.ResetPath();

            if (dist <= preferredRange * 1.15f && _cooldownTimer <= 0f)
                Fire();
        }

        private void Reposition(float desiredDist)
        {
            Vector3 away = transform.position - _target.position; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            Vector3 dest = _target.position + away.normalized * desiredDist;
            if (NavMesh.SamplePosition(dest, out var hit, 4f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        private void Fire()
        {
            _cooldownTimer = fireCooldown;
            if (animator != null) animator.SetTrigger(FireHash);
            if (muzzlePoint == null || _target == null) return;

            // Luân phiên giữa các khẩu đang cầm (dual-wield).
            var gun = _guns[_nextGun % _guns.Count];
            _nextGun++;

            var ammo = FindAmmo(gun.caliber);
            float damage = ammo != null ? ammo.damage : 20f;
            int pellets = ammo != null ? Mathf.Max(1, ammo.pellets) : 1; // Shotgun buckshot toả nhiều viên
            float spread = pellets > 1 && ammo != null ? ammo.spreadAngle : 0f;

            Vector3 baseDir = (_target.position + Vector3.up * 1.0f - muzzlePoint.position).normalized;
            for (int i = 0; i < pellets; i++)
                FireHitscan(baseDir, spread, damage, ammo);

            NoiseSystem.EmitNoise(transform.position, 1.6f); // tiếng súng -> cannibal khác có thể nghe thấy
        }

        private void FireHitscan(Vector3 baseDir, float spread, float damage, AmmoItemData ammo)
        {
            Vector3 dir = baseDir;
            if (spread > 0f)
                dir = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f) * dir;

            if (!Physics.Raycast(muzzlePoint.position, dir, out var hit, hitscanRange, hitMask, QueryTriggerInteraction.Ignore))
                return;

            var choppable = hit.collider.GetComponentInParent<IChoppable>();
            if (choppable != null && choppable.CanBeChopped())
            {
                choppable.ApplyChop(damage, transform.root);
            }
            else
            {
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                    damageable.DealDamage(damage, dir, transform.root, false);
            }

            if (ammo != null && ammo.impactVfxPrefab != null)
                Instantiate(ammo.impactVfxPrefab, hit.point, Quaternion.LookRotation(hit.normal));
        }

        private AmmoItemData FindAmmo(AmmoCaliber caliber)
        {
            if (ammoDatabase == null) return null;
            foreach (var a in ammoDatabase)
                if (a != null && a.caliber == caliber) return a;
            return null;
        }
    }
}
