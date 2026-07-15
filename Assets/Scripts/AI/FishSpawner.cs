using System.Collections.Generic;
using UnityEngine;

namespace TheForest.AI
{
    /// <summary>Spawn cá trong một vùng nước (box), gán tuyến bơi sẵn. Giữ số cá ổn định.</summary>
    public class FishSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject fishPrefab;
        [SerializeField] private Transform[] sharedSwimPoints; // tuyến bơi dùng chung
        [SerializeField] private TheForest.World.SeasonSystem seasons;
        [SerializeField] private int targetCount = 5;
        [SerializeField] private Vector3 areaSize = new Vector3(20, 1, 20);
        [SerializeField] private float waterY = 0.5f;
        [SerializeField] private float respawnInterval = 8f;

        private readonly List<GameObject> _fish = new List<GameObject>();
        private float _timer;

        private void Awake()
        {
            if (seasons == null) seasons = FindFirstObjectByType<TheForest.World.SeasonSystem>();
        }

        private void Update()
        {
            _fish.RemoveAll(f => f == null);
            _timer -= Time.deltaTime;
            if (_timer <= 0f && _fish.Count < CurrentTargetCount())
            {
                _timer = respawnInterval;
                SpawnOne();
            }
        }

        private void SpawnOne()
        {
            if (fishPrefab == null) return;
            Vector3 pos = transform.position + new Vector3(
                Random.Range(-areaSize.x / 2, areaSize.x / 2),
                0f,
                Random.Range(-areaSize.z / 2, areaSize.z / 2));
            pos.y = waterY;

            var go = Instantiate(fishPrefab, pos, Quaternion.identity);
            _fish.Add(go);
        }

        private int CurrentTargetCount()
        {
            float multiplier = seasons != null ? seasons.FishMultiplier :
                (TheForest.World.SeasonSystem.Instance != null ? TheForest.World.SeasonSystem.Instance.FishMultiplier : 1f);
            return Mathf.Max(0, Mathf.RoundToInt(targetCount * multiplier));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0, 0.5f, 1f, 0.3f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * waterY, areaSize);
        }
    }
}
