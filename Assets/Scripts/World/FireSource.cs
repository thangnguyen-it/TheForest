using System.Collections.Generic;
using UnityEngine;

namespace TheForest.World
{
    /// <summary>
    /// Nguồn lửa/effigy cháy khiến cannibal sợ. Gắn lên campfire, đuốc cắm, effigy đang cháy.
    /// Tự đăng ký vào FireRegistry khi bật, hủy đăng ký khi tắt.
    /// </summary>
    public class FireSource : MonoBehaviour
    {
        [Tooltip("Bán kính khiến cannibal sợ và né ra.")]
        [SerializeField] private float fearRadius = 8f;
        [Tooltip("Cường độ sợ (1 = thường, effigy lớn có thể >1).")]
        [SerializeField] private float intensity = 1f;
        [Tooltip("Đang cháy hay không (đuốc tắt thì off).")]
        [SerializeField] private bool isBurning = true;

        public float FearRadius => fearRadius;
        public float Intensity => intensity;
        public bool IsBurning => isBurning;
        public Vector3 Position => transform.position;

        private void OnEnable() => FireRegistry.Register(this);
        private void OnDisable() => FireRegistry.Unregister(this);

        public void SetBurning(bool burning)
        {
            isBurning = burning;
            if (burning) FireRegistry.Register(this);
            else FireRegistry.Unregister(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, fearRadius);
        }
    }

    /// <summary>Danh bạ nguồn lửa đang cháy để AI truy vấn nhanh.</summary>
    public static class FireRegistry
    {
        private static readonly List<FireSource> _fires = new List<FireSource>();
        public static IReadOnlyList<FireSource> Fires => _fires;

        public static void Register(FireSource f)
        {
            if (f != null && !_fires.Contains(f)) _fires.Add(f);
        }
        public static void Unregister(FireSource f) => _fires.Remove(f);

        /// <summary>
        /// Trả nguồn lửa khiến vị trí pos sợ nhất (gần & mạnh nhất), null nếu an toàn.
        /// out fearStrength: 0..>1 mức sợ (càng gần lửa càng cao).
        /// </summary>
        public static FireSource GetStrongestFearAt(Vector3 pos, out float fearStrength)
        {
            fearStrength = 0f;
            FireSource best = null;
            for (int i = _fires.Count - 1; i >= 0; i--)
            {
                var f = _fires[i];
                if (f == null) { _fires.RemoveAt(i); continue; }
                if (!f.IsBurning) continue;

                float dist = Vector3.Distance(pos, f.Position);
                if (dist > f.FearRadius) continue;

                // càng gần tâm lửa càng sợ
                float s = (1f - dist / f.FearRadius) * f.Intensity;
                if (s > fearStrength) { fearStrength = s; best = f; }
            }
            return best;
        }
    }
}
