using System.Collections.Generic;
using UnityEngine;

namespace TheForest.AI
{
    /// <summary>
    /// Điểm trèo cây cho cannibal: gồm vị trí chân cây (NavMesh) và vị trí trên cao (perch).
    /// Cannibal đi tới base, "leo" lên perch (lerp), quan sát; thấy player thì nhảy xuống.
    /// </summary>
    public class TreeClimbSpot : MonoBehaviour
    {
        [Tooltip("Vị trí dưới gốc (phải nằm trên NavMesh).")]
        [SerializeField] private Transform basePoint;
        [Tooltip("Vị trí ngồi quan sát trên cao.")]
        [SerializeField] private Transform perchPoint;

        public Vector3 BasePosition => basePoint != null ? basePoint.position : transform.position;
        public Vector3 PerchPosition => perchPoint != null ? perchPoint.position : transform.position + Vector3.up * 4f;
        public bool IsOccupied { get; private set; }

        private void OnEnable() => ClimbSpotRegistry.Register(this);
        private void OnDisable() => ClimbSpotRegistry.Unregister(this);

        public bool TryOccupy() { if (IsOccupied) return false; IsOccupied = true; return true; }
        public void Vacate() => IsOccupied = false;
    }

    public static class ClimbSpotRegistry
    {
        private static readonly List<TreeClimbSpot> _spots = new List<TreeClimbSpot>();

        public static void Register(TreeClimbSpot s) { if (s != null && !_spots.Contains(s)) _spots.Add(s); }
        public static void Unregister(TreeClimbSpot s) => _spots.Remove(s);

        public static TreeClimbSpot FindFreeNear(Vector3 pos, float maxDist)
        {
            TreeClimbSpot best = null;
            float bestSqr = maxDist * maxDist;
            for (int i = _spots.Count - 1; i >= 0; i--)
            {
                var s = _spots[i];
                if (s == null) { _spots.RemoveAt(i); continue; }
                if (s.IsOccupied) continue;

                float d = (s.BasePosition - pos).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = s; }
            }
            return best;
        }
    }
}