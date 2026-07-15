using UnityEngine;
using TheForest.Interaction;
using TheForest.Items;

namespace TheForest.AI
{
    /// <summary>
    /// Mũi tên: bay theo vận tốc (trọng lực), trúng -> damage + hiệu ứng theo ArrowType.
    /// Normal: damage. Poison: DoT + slow. Fire: damage + để lại FireZone.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Arrow : MonoBehaviour
    {
        private float _damage;
        private ArrowItemData _arrowData;
        private Transform _owner;
        private bool _spent;

        [SerializeField] private float lifeTime = 8f;
        [SerializeField] private LayerMask hitMask = ~0;

        public void Launch(Vector3 velocity, float damage, ArrowItemData arrowData, Transform owner)
        {
            _damage = damage; _arrowData = arrowData; _owner = owner;
            var rb = GetComponent<Rigidbody>();
            rb.linearVelocity = velocity; // Unity 6
            Destroy(gameObject, lifeTime);
        }

        private void Update()
        {
            // Hướng mũi tên theo vận tốc (đẹp)
            var rb = GetComponent<Rigidbody>();
            if (rb.linearVelocity.sqrMagnitude > 0.1f)
                transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);
        }

        private void OnCollisionEnter(Collision col)
        {
            if (_spent) return;
            if (_owner != null && col.transform.IsChildOf(_owner)) return;
            _spent = true;

            Transform root = col.transform.root;

            // Trúng động vật/địch (IChoppable)
            var chop = col.collider.GetComponentInParent<IChoppable>();
            if (chop != null && chop.CanBeChopped())
            {
                chop.ApplyChop(_damage, _owner);
                ApplyArrowEffect(root);
            }
            else
            {
                // Trúng player? (nếu kẻ địch bắn tên) hoặc môi trường
                var dmgable = col.collider.GetComponentInParent<IDamageable>();
                if (dmgable != null)
                {
                    Vector3 dir = (root.position - transform.position).normalized;
                    dmgable.DealDamage(_damage, dir, _owner, false);
                }
            }

            // Fire arrow: để lại vùng cháy tại điểm chạm
            if (_arrowData != null && _arrowData.arrowType == ArrowType.Fire
                && _arrowData.fireZonePrefab != null)
            {
                Vector3 p = col.contacts.Length > 0 ? col.contacts[0].point : transform.position;
                Instantiate(_arrowData.fireZonePrefab, p, Quaternion.identity);
            }

            // Cắm tên lại (tùy chọn nhặt lại) hoặc hủy
            Destroy(gameObject, 0.05f);
        }

        private void ApplyArrowEffect(Transform target)
        {
            if (_arrowData == null) return;

            if (_arrowData.arrowType == ArrowType.Poison)
            {
                var status = target.GetComponentInChildren<AnimalStatus>();
                if (status == null) status = target.GetComponent<AnimalStatus>();
                if (status != null)
                    status.ApplyPoisonAndSlow(_arrowData.poisonDps, _arrowData.poisonDuration, _arrowData.slowFactor);
            }
            // Fire effect handled qua FireZone ở OnCollisionEnter
        }
    }
}
