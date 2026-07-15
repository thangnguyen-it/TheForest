using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace TheForest.AI
{
    /// <summary>
    /// Spawn Puffy theo NHÓM NHỎ quanh player — GDD thật xác nhận chúng hiếm khi đơn độc và MutantAI
    /// cũng mô phỏng "hiếm khi dấn thân một mình" (xem soloFleeChance), nên population hợp lý phải LUÔN
    /// spawn theo cụm ≥2, không lẻ tẻ từng con một như AnimalSpawner.
    ///
    /// LƯU Ý PHẠM VI: dự án CHƯA có hệ Cave (Giai đoạn 6 của roadmap) — spawner này tạm dùng một điểm +
    /// bán kính quanh player để mô phỏng "một khu vực hang", KHÔNG phải spawn thật bên trong hình học
    /// hang. Khi Giai đoạn 6 xây xong, chỉ cần đổi FindSpawnPoint() để lấy điểm từ trong bounds hang
    /// thay vì quanh player — phần còn lại (population cap, cụm, tough variant) giữ nguyên.
    ///
    /// Biến thể Tough (Spotted/Blue/Glowing) xuất hiện ở hang sâu/về sau. LƯU Ý (fidelity): KHÔNG nguồn
    /// chính thức nào chốt "mốc ngày" cụ thể cho biến thể này — toughVariantMinDay chỉ là XẤP XỈ để
    /// biến thể mạnh không tràn ra sớm, KHÔNG phải con số đã kiểm chứng. Chỉnh tự do theo cân bằng.
    /// </summary>
    public class MutantSpawner : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private Transform player;
        [Tooltip("Để trống sẽ tự lấy AggressionManager.Instance — dùng CurrentDay có sẵn cho mốc mở Tough variant.")]
        [SerializeField] private AggressionManager aggressionManagerForDay;

        [Header("Prefab (kéo cả biến thể nam/nữ vào đây, chọn ngẫu nhiên)")]
        [SerializeField] private GameObject[] puffyPrefabs;

        [Header("Vùng sinh (tạm thời quanh player — xem ghi chú phạm vi ở trên)")]
        [SerializeField] private Vector2 spawnDistanceRange = new Vector2(15f, 30f);
        [SerializeField] private float minPlayerVisibleDist = 14f;
        [SerializeField] private float navSampleRadius = 5f;

        [Header("Spawn theo cụm")]
        [SerializeField] private float spawnInterval = 30f;
        [SerializeField] private Vector2Int groupSizeRange = new Vector2Int(2, 4);
        [SerializeField] private int hardCap = 10;

        [Header("Biến thể Tough (Blue/Spotted/Glowing)")]
        [Range(0f, 1f)][SerializeField] private float toughVariantChance = 0.12f;
        [Tooltip("Glowing Puffy xác nhận từ Ngày 30 (GDD thật). Blue/Spotted có thể xuất hiện sớm hơn " +
                 "trong Cultist Cave cụ thể — đơn giản hoá thành 1 mốc chung ở đây, tinh chỉnh sau nếu cần.")]
        [SerializeField] private int toughVariantMinDay = 30;

        private float _timer;
        private readonly List<MutantAI> _alive = new List<MutantAI>();

        private void Awake()
        {
            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) player = p.transform;
            }
            if (aggressionManagerForDay == null) aggressionManagerForDay = AggressionManager.Instance;
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

        private void TrySpawnGroup()
        {
            if (player == null || puffyPrefabs == null || puffyPrefabs.Length == 0) return;
            if (_alive.Count >= hardCap) return;
            if (!FindSpawnPoint(out Vector3 center)) return;

            int size = Mathf.Min(Random.Range(groupSizeRange.x, groupSizeRange.y + 1), hardCap - _alive.Count);

            for (int i = 0; i < size; i++)
            {
                Vector3 pos = center + Random.insideUnitSphere * 3f; pos.y = center.y;
                if (!NavMesh.SamplePosition(pos, out var hit, navSampleRadius, NavMesh.AllAreas)) continue;

                var prefab = puffyPrefabs[Random.Range(0, puffyPrefabs.Length)];
                var go = Instantiate(prefab, hit.position, Quaternion.identity);
                var ai = go.GetComponent<MutantAI>();
                if (ai == null) { Destroy(go); continue; }

                if (CanRollToughVariant() && Random.value < toughVariantChance)
                    ai.SetToughVariant(true);

                _alive.Add(ai);
            }
        }

        private bool CanRollToughVariant()
        {
            int day = aggressionManagerForDay != null ? aggressionManagerForDay.CurrentDay : 1;
            return day >= toughVariantMinDay;
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
                    result = hit.position;
                    return true;
                }
            }
            return false;
        }
    }
}
