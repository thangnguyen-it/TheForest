using System.Collections.Generic;
using UnityEngine;

namespace TheForest.World
{
    /// <summary>
    /// Rải cây/đá procedural lên Terrain. Chạy trong Editor (nút Generate ở custom inspector),
    /// bake prefab THẬT thẳng vào scene (không tính lại lúc runtime) — đúng GDD.
    /// Pipeline: Poisson -> density noise -> lọc dốc/độ cao -> exclusion -> Instantiate.
    /// </summary>
    public class ScatterManager : MonoBehaviour
    {
        [Header("Terrain")]
        [SerializeField] private Terrain terrain;

        [Header("Prefab rải (chọn ngẫu nhiên trong list)")]
        [SerializeField] private GameObject[] prefabs;

        [Header("Poisson")]
        [Tooltip("Bán kính tối thiểu giữa 2 vật. Đá: lớn (thưa); cỏ: nhỏ (dày); cây: trung bình.")]
        [SerializeField] private float minRadius = 8f;
        [SerializeField] private int seed = 12345;

        [Header("Lọc địa hình")]
        [Tooltip("Độ dốc tối đa cho phép đặt (độ). Dốc hơn -> bỏ qua.")]
        [SerializeField] private float maxSlope = 35f;
        [Tooltip("Độ cao tối thiểu (tránh đặt dưới nước/bãi biển). Theo world Y.")]
        [SerializeField] private float minHeight = 2f;
        [SerializeField] private float maxHeight = 200f;

        [Header("Density noise (cụm dày/thưa)")]
        [Tooltip("Tần số noise mật độ. Nhỏ = cụm to.")]
        [SerializeField] private float densityNoiseScale = 0.01f;
        [Tooltip("Ngưỡng: điểm có noise dưới ngưỡng bị loại -> tạo khoảng trống.")]
        [SerializeField, Range(0f, 1f)] private float densityThreshold = 0.35f;

        [Header("Biến tấu")]
        [SerializeField] private bool randomYRotation = true;
        [SerializeField] private Vector2 scaleRange = new Vector2(0.9f, 1.2f);

        [Header("Output")]
        [Tooltip("Object cha chứa các vật đã rải, để dễ xóa/quản lý")]
        [SerializeField] private Transform container;

        // ===================== SINH (gọi từ Editor) =====================
        public void Generate()
        {
            if (terrain == null || prefabs == null || prefabs.Length == 0)
            {
                Debug.LogWarning("[Scatter] Thiếu terrain hoặc prefabs.");
                return;
            }

            Clear();

            var data = terrain.terrainData;
            float w = data.size.x;
            float l = data.size.z;
            Vector3 origin = terrain.transform.position;

            var points = PoissonDiscSampler.Generate(w, l, minRadius, 30, seed);
            int placed = 0;

            foreach (var p in points)
            {
                // Tọa độ chuẩn hóa 0..1 trên terrain
                float nx = p.x / w;
                float nz = p.y / l;

                // 1) Lọc độ dốc
                float steepness = data.GetSteepness(nx, nz);
                if (steepness > maxSlope) continue;

                // 2) Lấy cao độ thực
                Vector3 worldPos = new Vector3(origin.x + p.x, 0f, origin.z + p.y);
                float y = terrain.SampleHeight(worldPos) + origin.y;
                if (y < minHeight || y > maxHeight) continue;
                worldPos.y = y;

                // 3) Density noise -> tạo cụm dày/thưa
                float noise = Mathf.PerlinNoise(
                    (origin.x + p.x) * densityNoiseScale,
                    (origin.z + p.y) * densityNoiseScale);
                if (noise < densityThreshold) continue;

                // 4) Exclusion mask (sông/đường/trại) — TODO: cắm mask khi có
                // if (IsExcluded(worldPos)) continue;

                // 5) Instantiate prefab thật
                var prefab = prefabs[Random.Range(0, prefabs.Length)];
                var go = InstantiatePrefab(prefab, worldPos);
                if (go == null) continue;

                if (randomYRotation)
                    go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                float s = Random.Range(scaleRange.x, scaleRange.y);
                go.transform.localScale *= s;

                if (container != null) go.transform.SetParent(container, true);
                placed++;
            }

            Debug.Log($"[Scatter] Đã rải {placed}/{points.Count} vật (seed {seed}, minRadius {minRadius}).");
        }

        public void Clear()
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
#if UNITY_EDITOR
                DestroyImmediate(container.GetChild(i).gameObject);
#else
                Destroy(container.GetChild(i).gameObject);
#endif
            }
        }

        private GameObject InstantiatePrefab(GameObject prefab, Vector3 pos)
        {
#if UNITY_EDITOR
            // Giữ liên kết prefab khi tạo trong Editor
            var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = pos;
            return go;
#else
            return Instantiate(prefab, pos, Quaternion.identity);
#endif
        }
    }
}
