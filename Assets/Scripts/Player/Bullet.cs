using UnityEngine;
using TheForest.Interaction;

namespace TheForest.AI
{
    /// <summary>
    /// Đạn súng — song song Arrow.cs (cùng namespace TheForest.AI, cùng thư mục, để nhất quán với
    /// convention có sẵn của dự án) nhưng đơn giản hơn: không status-effect theo loại như Poison/Fire
    /// arrow. Stun Gun tái dùng CHÍNH class này (isStun=true): KHÔNG gây damage, thay vào đó gọi
    /// CannibalAI.Stun() (đã có sẵn) hoặc MutantAI.Stun() (vừa thêm ở Giai đoạn 3) nếu trúng.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private float lifeTime = 4f;

        private float _damage;
        private bool _isStun;
        private float _stunDuration;
        private Transform _owner;
        private bool _spent;

        public void Launch(Vector3 velocity, float damage, bool isStun, float stunDuration, Transform owner)
        {
            _damage = damage; _isStun = isStun; _stunDuration = stunDuration; _owner = owner;
            var rb = GetComponent<Rigidbody>();
            rb.linearVelocity = velocity; // Unity 6
            transform.rotation = Quaternion.LookRotation(velocity.normalized);
            Destroy(gameObject, lifeTime);
        }

        private void OnCollisionEnter(Collision col)
        {
            if (_spent) return;
            if (_owner != null && col.transform.IsChildOf(_owner)) return;
            _spent = true;

            if (_isStun) ApplyStun(col.collider);
            else ApplyDamage(col.collider);

            Destroy(gameObject, 0.05f);
        }

        private void ApplyDamage(Collider col)
        {
            // Thứ tự thử giống hệt Arrow.cs: IChoppable trước (Cannibal/Mutant/Virginia), IDamageable sau (Player/Virginia)
            var choppable = col.GetComponentInParent<IChoppable>();
            if (choppable != null && choppable.CanBeChopped())
            {
                choppable.ApplyChop(_damage, _owner);
                return;
            }

            var damageable = col.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                Vector3 dir = (col.transform.position - transform.position).normalized;
                damageable.DealDamage(_damage, dir, _owner, false);
            }
        }

        private void ApplyStun(Collider col)
        {
            var cannibal = col.GetComponentInParent<CannibalAI>();
            if (cannibal != null) { cannibal.Stun(_stunDuration); return; }

            var mutant = col.GetComponentInParent<MutantAI>();
            if (mutant != null) mutant.Stun(_stunDuration);

            // Player/Virginia: game gốc không xác nhận Stun Gun tự bắn được vào chính mình/đồng minh — bỏ qua có chủ đích.
        }
    }
}
