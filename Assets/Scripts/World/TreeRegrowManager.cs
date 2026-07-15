using System.Collections.Generic;
using UnityEngine;

namespace TheForest.World
{
    /// <summary>
    /// Quản lý vòng mọc lại của cây theo GDD:
    /// mỗi khi người chơi ngủ (sang ngày mới), 10% số gốc cây (Stump) được chuyển về Standing.
    /// Theo dõi toàn bộ TreeCutting trong scene; nghe DayNightCycle.OnNewDay.
    /// Gốc đã bị đào (Removed) đã tự unregister -> không bao giờ mọc lại.
    /// </summary>
    public class TreeRegrowManager : MonoBehaviour
    {
        public static TreeRegrowManager Instance { get; private set; }

        [Header("Tham chiếu")]
        [Tooltip("Chu kỳ ngày/đêm để nghe sự kiện sang ngày mới (ngủ).")]
        [SerializeField] private DayNightCycle dayNight;

        [Header("Regrow")]
        [Tooltip("Tỉ lệ gốc cây mọc lại mỗi lần ngủ (0..1). GDD = 10%.")]
        [SerializeField, Range(0f, 1f)] private float regrowFraction = 0.1f;
        [SerializeField] private bool regrowEnabled = true;

        private readonly List<TreeCutting> _trees = new List<TreeCutting>();
        private readonly List<TreeCutting> _stumps = new List<TreeCutting>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            if (dayNight != null) dayNight.OnNewDay += HandleNewDay;
        }

        private void OnDisable()
        {
            if (dayNight != null) dayNight.OnNewDay -= HandleNewDay;
            if (Instance == this) Instance = null;
        }

        // ===================== ĐĂNG KÝ CÂY =====================
        public void Register(TreeCutting tree)
        {
            if (tree != null && !_trees.Contains(tree)) _trees.Add(tree);
        }

        public void Unregister(TreeCutting tree)
        {
            _trees.Remove(tree);
            _stumps.Remove(tree);
        }

        /// <summary>TreeCutting gọi khi vừa đổ -> thêm vào danh sách gốc chờ mọc lại.</summary>
        public void NotifyFelled(TreeCutting tree)
        {
            if (tree != null && !_stumps.Contains(tree)) _stumps.Add(tree);
        }

        // ===================== VÒNG REGROW =====================
        private void HandleNewDay(int dayNumber)
        {
            if (!regrowEnabled) return;
            RegrowStumps();
        }

        /// <summary>Chuyển ngẫu nhiên regrowFraction số gốc cây về Standing.</summary>
        public void RegrowStumps()
        {
            // Lọc bỏ cây đã bị đào/hủy (không còn Stump)
            _stumps.RemoveAll(t => t == null || t.State != TreeState.Stump);
            if (_stumps.Count == 0) return;

            int regrowCount = Mathf.Max(1, Mathf.RoundToInt(_stumps.Count * regrowFraction));

            // Shuffle nhẹ rồi lấy regrowCount đầu (Fisher-Yates một phần)
            for (int i = 0; i < regrowCount && _stumps.Count > 0; i++)
            {
                int idx = Random.Range(i, _stumps.Count);
                (_stumps[i], _stumps[idx]) = (_stumps[idx], _stumps[i]);
            }

            int regrown = 0;
            for (int i = 0; i < regrowCount && i < _stumps.Count; i++)
            {
                _stumps[i].Regrow();
                regrown++;
            }

            // Bỏ những cây vừa mọc lại khỏi danh sách stump
            _stumps.RemoveAll(t => t == null || t.State != TreeState.Stump);
            Debug.Log($"[Regrow] {regrown} gốc cây mọc lại (còn {_stumps.Count} gốc).");
        }
    }
}