using System.Collections.Generic;
using UnityEngine;
using TheForest.Interaction;

namespace TheForest.AI
{
    /// <summary>
    /// Vùng lửa trên mặt đất gây DoT cho thực thể trong bán kính. Tự tắt sau duration.
    /// Tích hợp FireSource để làm bọn Cannibal sợ hãi né ra.
    /// Tích hợp cờ cháy (Ignite) cho Player và hệ số sát thương theo độ khó cho Cannibal.
    /// </summary>
    public class FireZone : MonoBehaviour
    {
        [SerializeField] private float radius = 2.5f;
        [SerializeField] private float duration = 5f;
        [Tooltip("Sát thương mỗi giây cho thực thể trong vùng.")]
        [SerializeField] private float damagePerSec = 6f;
        [Tooltip("Tick áp damage mỗi bao nhiêu giây.")]
        [SerializeField] private float tickInterval = 0.5f;
        [SerializeField] private LayerMask affectMask = ~0;
        [SerializeField] private GameObject fireVfx;

        private Transform _owner;     // ai tạo ra (đạn của Fire Thrower)
        private float _life;
        private float _tickTimer;

        public void Init(float r, float dur, float dps, Transform owner)
        {
            radius = r; duration = dur; damagePerSec = dps; _owner = owner;
        }

        private void Start()
        {
            _life = duration;
            if (fireVfx != null)
            {
                var fx = Instantiate(fireVfx, transform.position, Quaternion.identity, transform);
                fx.transform.localScale = Vector3.one * radius;
            }

            // ==========================================
            // Tìm mạch hù dọa (FireSource) và bật nó lên 
            // ==========================================
            var fs = GetComponent<TheForest.World.FireSource>();
            if (fs != null) fs.SetBurning(true);
        }

        private void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f) { Destroy(gameObject); return; }

            _tickTimer -= Time.deltaTime;
            if (_tickTimer <= 0f)
            {
                _tickTimer = tickInterval;
                ApplyTick();
            }
        }

        private void ApplyTick()
        {
            float dmg = damagePerSec * tickInterval;
            var hits = Physics.OverlapSphere(transform.position, radius, affectMask, QueryTriggerInteraction.Ignore);

            var done = new HashSet<int>(); // tránh trúng 2 lần cùng 1 root
            foreach (var col in hits)
            {
                Transform root = col.transform.root;
                int id = root.GetInstanceID();
                if (done.Contains(id)) continue;
                done.Add(id);

                // 1. Áp sát thương lên Player
                var dmgable = col.GetComponentInParent<IDamageable>();
                if (dmgable != null)
                {
                    Vector3 dir = (root.position - transform.position).normalized;
                    dmgable.DealDamage(dmg, dir, _owner, false);

                    // Đốt cháy player thêm 2s sau khi bước ra khỏi vũng lửa (kích hoạt HUD/DoT)
                    var stats = col.GetComponentInParent<TheForest.Player.SurvivalStats>();
                    if (stats != null) stats.Ignite(2f);

                    continue;
                }

                // 2. Áp sát thương lên Cannibal (Chịu ảnh hưởng hệ số độ khó từ GDD)
                var chop = col.GetComponentInParent<TheForest.Interaction.IChoppable>();
                if (chop != null && chop.CanBeChopped())
                {
                    // Lấy hệ số kháng lửa của mutant tùy theo độ khó hiện tại (Normal = 1x, Hard = 0.5x)
                    float fireDmg = dmg * TheForest.World.GameDifficulty.FireOnMutant;
                    chop.ApplyChop(fireDmg, _owner);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}