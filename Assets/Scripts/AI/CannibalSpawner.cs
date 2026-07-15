using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace TheForest.AI
{
    /// <summary>
    /// Spawn cannibal theo vùng dựa trên unlock + aggression (GDD):
    /// - Loại địch: chỉ tới tribe đã unlock theo ngày.
    /// - Số lượng / cỡ nhóm: scale theo effective aggression của vùng.
    /// - Spawn quanh player nhưng NGOÀI tầm nhìn, trên NavMesh; gán patrol route.
    /// - Giữ population mềm (maxAlive) tránh lag.
    /// </summary>
    public class CannibalSpawner : MonoBehaviour
    {
        [Header("Vùng")]
        [SerializeField] private int zoneId = 0;
        [SerializeField] private Transform player;

        [Header("Configs (các biến thể có thể spawn)")]
        [SerializeField] private CannibalConfig[] configs;

        [Header("Patrol")]
        [Tooltip("Các waypoint dùng chung cho vùng này.")]
        [SerializeField] private Transform[] patrolPoints;

        [Header("Spawn")]
        [SerializeField] private float spawnInterval = 20f;
        [Tooltip("Khoảng cách spawn quanh player.")]
        [SerializeField] private Vector2 spawnDistanceRange = new Vector2(25f, 45f);
        [Tooltip("Không spawn nếu trong nón nhìn / quá gần player.")]
        [SerializeField] private float minPlayerVisibleDist = 20f;
        [SerializeField] private float navSampleRadius = 6f;

        [Header("Population (scale theo aggression)")]
        [SerializeField] private int baseMaxAlive = 3;
        [Tooltip("Mỗi điểm aggression thêm bao nhiêu slot.")]
        [SerializeField] private int alivePerAggression = 2;
        [SerializeField] private int hardCap = 12;

        [Header("Cỡ nhóm")]
        [SerializeField] private Vector2Int groupSizeRange = new Vector2Int(1, 3);
        [Tooltip("Aggression cao -> nhóm to hơn (cộng vào max).")]
        [SerializeField] private int extraGroupPerAggression = 1;

        private float _timer;
        private readonly List<CannibalAI> _alive = new List<CannibalAI>();

        private void Awake()
        {
            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) player = p.transform;
            }
        }

        private void Update()
        {
            _alive.RemoveAll(a => a == null);

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timer = spawnInterval;
                TrySpawnGroup();
            }
        }

        private float Aggression() =>
            AggressionManager.Instance != null
                ? AggressionManager.Instance.GetEffectiveAggression(zoneId) : 1f;

        private int CurrentMaxAlive()
        {
            float aggr = Aggression();
            int max = baseMaxAlive + Mathf.RoundToInt((aggr - 1f) * alivePerAggression);
            return Mathf.Clamp(max, baseMaxAlive, hardCap);
        }

        private void TrySpawnGroup()
        {
            if (player == null || configs == null || configs.Length == 0) return;
            if (_alive.Count >= CurrentMaxAlive()) return;

            if (!FindSpawnPoint(out Vector3 spawnPos)) return;

            // Cỡ nhóm theo aggression
            float aggr = Aggression();
            int extra = Mathf.RoundToInt((aggr - 1f) * extraGroupPerAggression);
            int size = Random.Range(groupSizeRange.x, groupSizeRange.y + 1) + Mathf.Max(0, extra);
            size = Mathf.Min(size, CurrentMaxAlive() - _alive.Count);
            if (size <= 0) return;

            // Tạo group object
            var groupGo = new GameObject($"CannibalGroup_Z{zoneId}");
            groupGo.transform.position = spawnPos;
            var group = groupGo.AddComponent<CannibalGroup>();
            group.ZoneId = zoneId;

            bool leaderAssigned = false;
            for (int i = 0; i < size; i++)
            {
                var cfg = PickConfig();
                if (cfg == null || cfg.prefab == null) continue;

                Vector3 pos = spawnPos + Random.insideUnitSphere * 3f; pos.y = spawnPos.y;
                if (!NavMesh.SamplePosition(pos, out var hit, navSampleRadius, NavMesh.AllAreas))
                    continue;

                var go = Instantiate(cfg.prefab, hit.position, Quaternion.identity);
                var ai = go.GetComponent<CannibalAI>();
                if (ai == null) { Destroy(go); continue; }

                ai.ConfigureFromSpawn(zoneId, patrolPoints, cfg);

                // Leader: ưu tiên config type Leader, hoặc chỉ định 1 con đầu nhóm lớn
                bool makeLeader = !leaderAssigned &&
                    (cfg.type == CannibalType.Leader || (size >= 3 && i == 0));
                group.AddMember(ai, makeLeader);
                if (makeLeader) leaderAssigned = true;

                _alive.Add(ai);
            }

            if (group.IsEmpty) Destroy(groupGo);
        }

        private CannibalConfig PickConfig()
        {
            int day = AggressionManager.Instance != null ? AggressionManager.Instance.CurrentDay : 1;
            var allowed = new List<CannibalConfig>();
            float totalW = 0f;
            foreach (var c in configs)
            {
                if (c == null || c.prefab == null) continue;
                if (c.minDay > day) continue;                 // chưa unlock theo ngày
                if (!IsTribeUnlocked(c.tribe)) continue;       // chưa unlock theo manager
                allowed.Add(c); totalW += c.spawnWeight;
            }
            if (allowed.Count == 0 || totalW <= 0f) return null;

            float r = Random.value * totalW;
            foreach (var c in allowed)
            {
                r -= c.spawnWeight;
                if (r <= 0f) return c;
            }
            return allowed[allowed.Count - 1];
        }

        private bool IsTribeUnlocked(CannibalTribe tribe)
        {
            var m = AggressionManager.Instance;
            if (m == null) return tribe == CannibalTribe.Regular || tribe == CannibalTribe.Starving;
            switch (tribe)
            {
                case CannibalTribe.Starving:
                case CannibalTribe.Regular: return true;
                case CannibalTribe.PaleSkinny: return m.IsPaleSkinnyUnlocked;
                case CannibalTribe.Pale: return m.IsPaleUnlocked;
                case CannibalTribe.Painted: return m.IsPaleUnlocked;   // giữa-cuối; tinh chỉnh nếu cần
                case CannibalTribe.Masked: return m.IsMaskedUnlocked;
                default: return true;
            }
        }

        private bool FindSpawnPoint(out Vector3 result)
        {
            result = Vector3.zero;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float dist = Random.Range(spawnDistanceRange.x, spawnDistanceRange.y);
                Vector2 c = Random.insideUnitCircle.normalized * dist;
                Vector3 candidate = player.position + new Vector3(c.x, 0f, c.y);

                if (Vector3.Distance(candidate, player.position) < minPlayerVisibleDist) continue;

                if (NavMesh.SamplePosition(candidate, out var hit, navSampleRadius, NavMesh.AllAreas))
                {
                    // (tùy chọn) kiểm tra ngoài nón nhìn của camera để không spawn trước mặt
                    result = hit.position;
                    return true;
                }
            }
            return false;
        }
    }
}
