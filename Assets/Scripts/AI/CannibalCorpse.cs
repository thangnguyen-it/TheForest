using System.Collections.Generic;
using UnityEngine;

namespace TheForest.AI
{
    public enum CorpseKind { Cannibal, Animal }

    /// <summary>
    /// Xác cannibal hoặc động vật. Đăng ký vào CorpseRegistry.
    /// Có thể bị khiêng đi (Cannibal thường) hoặc bị ăn (Starving/Động vật đói).
    /// </summary>
    public class CannibalCorpse : MonoBehaviour
    {
        [Header("Thông tin xác")]
        [SerializeField] private CorpseKind kind = CorpseKind.Cannibal;

        [Tooltip("Lượng máu Starving hồi khi ăn hết xác này.")]
        [SerializeField] private float nutrition = 20f;

        [Tooltip("Số lần ăn để hết xác.")]
        [SerializeField] private int bites = 3;

        public CorpseKind Kind => kind;
        public float Nutrition => nutrition;

        public bool IsClaimed { get; private set; }
        public bool IsBuried { get; private set; }
        public bool IsEaten { get; private set; }

        private int _bitesLeft = -1;

        private void OnEnable() => CorpseRegistry.Register(this);
        private void OnDisable() => CorpseRegistry.Unregister(this);

        public bool TryClaim()
        {
            if (IsClaimed || IsBuried || IsEaten) return false;
            IsClaimed = true;
            return true;
        }

        public void Release()
        {
            if (!IsBuried && !IsEaten) IsClaimed = false;
        }

        public void Bury()
        {
            IsBuried = true;
            CorpseRegistry.Unregister(this);
            Destroy(gameObject, 1.5f);
        }

        /// <summary>Một lần cắn. Trả nutrition mỗi lần; khi hết -> đánh dấu eaten.</summary>
        public float EatBite()
        {
            if (_bitesLeft < 0) _bitesLeft = Mathf.Max(1, bites);
            _bitesLeft--;

            if (_bitesLeft <= 0)
            {
                IsEaten = true;
                CorpseRegistry.Unregister(this);
                Destroy(gameObject, 0.5f);
            }
            return nutrition / Mathf.Max(1, bites);
        }

        public void AttachTo(Transform carrier) => transform.SetParent(carrier, true);
        public void Detach() => transform.SetParent(null, true);
    }

    public static class CorpseRegistry
    {
        private static readonly List<CannibalCorpse> _corpses = new List<CannibalCorpse>();

        public static void Register(CannibalCorpse c) { if (c != null && !_corpses.Contains(c)) _corpses.Add(c); }
        public static void Unregister(CannibalCorpse c) => _corpses.Remove(c);

        public static CannibalCorpse FindUnclaimedNear(Vector3 pos, float maxDist)
        {
            CannibalCorpse best = null;
            float bestSqr = maxDist * maxDist;
            for (int i = _corpses.Count - 1; i >= 0; i--)
            {
                var c = _corpses[i];
                if (c == null) { _corpses.RemoveAt(i); continue; }
                if (c.IsClaimed || c.IsBuried || c.IsEaten) continue;

                float d = (c.transform.position - pos).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = c; }
            }
            return best;
        }
    }
}