using UnityEngine;
using TheForest.Interaction;

namespace TheForest.AI
{
    /// <summary>
    /// Đạn lửa của Fire Thrower. Bay theo vận tốc ban đầu (có trọng lực),
    /// trúng player -> DealDamage; trúng đất/vật -> nổ tắt. Tùy chọn để lại vùng cháy.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FireProjectile : MonoBehaviour
    {
        [SerializeField] private float damage = 10f;
        [SerializeField] private float lifeTime = 6f;
        [SerializeField] private GameObject impactVfx;
        [SerializeField] private LayerMask hitMask = ~0;
        [Header("Vùng cháy để lại")]
        [SerializeField] private GameObject fireZonePrefab;
        [SerializeField] private float zoneRadius = 2.5f;
        [SerializeField] private float zoneDuration = 5f;
        [SerializeField] private float zoneDps = 6f;

        private void OnCollisionEnter(Collision col)
        {
            if (_spent) return;
            if (_owner != null && col.transform.IsChildOf(_owner)) return;
            _spent = true;

            // damage trực tiếp khi trúng (như cũ)
            var dmgable = col.collider.GetComponentInParent<IDamageable>();
            if (dmgable != null)
            {
                Vector3 dir = (col.transform.position - transform.position).normalized;
                dmgable.DealDamage(damage, dir, _owner, false);
            }

            if (impactVfx != null) { var fx = Instantiate(impactVfx, transform.position, Quaternion.identity); Destroy(fx, 3f); }

            // để lại vùng cháy tại điểm chạm đất
            if (fireZonePrefab != null)
            {
                Vector3 groundPos = col.contacts.Length > 0 ? col.contacts[0].point : transform.position;
                var zoneGo = Instantiate(fireZonePrefab, groundPos, Quaternion.identity);
                var zone = zoneGo.GetComponent<TheForest.AI.FireZone>();
                if (zone != null) zone.Init(zoneRadius, zoneDuration, zoneDps, _owner);
            }

            Destroy(gameObject);
        }

        private Transform _owner;
        private bool _spent;

        public void Launch(Vector3 velocity, float dmg, Transform owner)
        {
            damage = dmg;
            _owner = owner;
            var rb = GetComponent<Rigidbody>();
            rb.linearVelocity = velocity;   // Unity 6: linearVelocity (thay velocity cũ)
            Destroy(gameObject, lifeTime);
        }

    }
}
