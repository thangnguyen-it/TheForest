using UnityEngine;
using TheForest.Interaction;
using TheForest.Items;

namespace TheForest.AI
{
    /// <summary>
    /// Cá bơi tuyến tính giữa các điểm trong vùng nước. Chết 1 đòn (IChoppable).
    /// Spear jab đâm trúng -> rơi 1 thịt (hoặc tự vào túi nếu là spear).
    /// Không dùng NavMesh (bơi tự do).
    /// </summary>
    public class FishAI : MonoBehaviour, IChoppable
    {
        [Header("Bơi")]
        [SerializeField] private Transform[] swimPoints; // tuyến bơi trong hồ
        [SerializeField] private float speed = 2.5f;
        [SerializeField] private float turnSpeed = 4f;
        [SerializeField] private float pointTolerance = 0.4f;

        [Header("Né player (nhẹ)")]
        [SerializeField] private float fleeRadius = 3f;
        [SerializeField] private float fleeSpeed = 5f;
        private Transform _player;

        [Header("Loot")]
        [SerializeField] private ItemData fishMeat;
        [SerializeField] private GameObject lootPickupPrefab;
        [SerializeField] private GameObject corpsePrefab; // corpse Animal (cho Starving) - tùy chọn

        private int _index;
        private bool _dead;

        private void Awake()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _player = p.transform;
        }

        private void Update()
        {
            if (_dead || swimPoints == null || swimPoints.Length == 0) return;

            // Né player nếu quá gần
            if (_player != null && Vector3.Distance(transform.position, _player.position) < fleeRadius)
            {
                Vector3 away = (transform.position - _player.position).normalized;
                Move(transform.position + away * 5f, fleeSpeed);
                return;
            }

            // Bơi tới điểm hiện tại
            Vector3 target = swimPoints[_index].position;
            Move(target, speed);
            if (Vector3.Distance(transform.position, target) < pointTolerance)
                _index = (_index + 1) % swimPoints.Length;
        }

        private void Move(Vector3 target, float spd)
        {
            Vector3 dir = (target - transform.position);
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion rot = Quaternion.LookRotation(dir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, turnSpeed * Time.deltaTime);
            }
            transform.position = Vector3.MoveTowards(transform.position, target, spd * Time.deltaTime);
        }

        // ===================== IChoppable =====================
        public bool CanBeChopped() => !_dead;

        public bool ApplyChop(float damage, Transform attacker)
        {
            if (_dead) return false;
            Die(attacker);
            return true; // cá chết 1 đòn
        }

        private void Die(Transform killer)
        {
            _dead = true;

            // Spear jab: cá "vào đầu giáo" -> cho thẳng vào túi người chém nếu có Inventory
            var inv = killer != null ? killer.GetComponent<TheForest.Player.Inventory>() : null;
            if (inv != null && fishMeat != null)
            {
                int leftover = inv.Add(fishMeat, 1);
                if (leftover == 0) { Destroy(gameObject); return; }
            }

            // Không nhặt được -> rơi pickup
            if (fishMeat != null && lootPickupPrefab != null)
            {
                var go = Instantiate(lootPickupPrefab, transform.position, Quaternion.identity);
                var pickup = go.GetComponent<ItemPickup>();
                if (pickup != null) pickup.Configure(fishMeat, 1);
            }

            if (corpsePrefab != null)
                Instantiate(corpsePrefab, transform.position, Quaternion.identity);

            Destroy(gameObject);
        }
    }
}
