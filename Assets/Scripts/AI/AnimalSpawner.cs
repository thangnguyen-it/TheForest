using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace TheForest.AI
{
    /// <summary>
    /// Spawn động vật quanh player (deer/rabbit/...) với mật độ scale theo difficulty
    /// (animalSpawnMult: Hard Survival ít hơn). Giữ population mềm theo maxAlive.
    /// </summary>
    public class AnimalSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class AnimalEntry
        {
            public AnimalKind kind = AnimalKind.Deer;
            public GameObject prefab;
            [Min(0f)] public float weight = 1f;
            [Tooltip("Chỉ spawn ban đêm? (raccoon).")]
            public bool nightOnly = false;
        }

        [Header("Tham chiếu")]
        [SerializeField] private Transform player;
        [SerializeField] private TheForest.World.DayNightCycle dayNight;
        [SerializeField] private TheForest.World.SeasonSystem seasons;

        [Header("Loài")]
        [SerializeField] private AnimalEntry[] animals;

        [Header("Spawn")]
        [SerializeField] private float spawnInterval = 12f;
        [SerializeField] private Vector2 spawnDistanceRange = new Vector2(18f, 35f);
        [SerializeField] private float minPlayerDist = 15f;
        [SerializeField] private float navSampleRadius = 5f;

        [Header("Population (scale theo difficulty)")]
        [SerializeField] private int baseMaxAlive = 6;
        [SerializeField] private int hardCap = 14;

        private float _timer;
        private readonly List<GameObject> _alive = new List<GameObject>();

        private void Awake()
        {
            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) player = p.transform;
            }
            if (seasons == null) seasons = FindFirstObjectByType<TheForest.World.SeasonSystem>();
        }

        private void Update()
        {
            _alive.RemoveAll(a => a == null);
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timer = spawnInterval;
                TrySpawn();
            }
        }

        private float SpawnMult =>
            TheForest.World.GameDifficulty.Current != null
                ? TheForest.World.GameDifficulty.Current.animalSpawnMult : 1f;

        private float SeasonAnimalMult =>
            seasons != null ? seasons.AnimalMultiplier :
            (TheForest.World.SeasonSystem.Instance != null ? TheForest.World.SeasonSystem.Instance.AnimalMultiplier : 1f);

        private int CurrentMaxAlive()
        {
            int max = Mathf.RoundToInt(baseMaxAlive * SpawnMult * SeasonAnimalMult);
            return Mathf.Clamp(max, 0, hardCap);
        }

        private void TrySpawn()
        {
            if (player == null || animals == null || animals.Length == 0) return;
            if (_alive.Count >= CurrentMaxAlive()) return;

            var entry = PickAnimal();
            if (entry == null || entry.prefab == null) return;

            if (!FindSpawnPoint(out Vector3 pos)) return;

            var go = Instantiate(entry.prefab, pos, Quaternion.Euler(0, Random.Range(0, 360f), 0));
            var ai = go.GetComponent<AnimalAI>();
            if (ai != null) ai.ConfigureKind(entry.kind);
            _alive.Add(go);
        }

        private AnimalEntry PickAnimal()
        {
            bool isNight = dayNight != null && dayNight.IsNight;
            var allowed = new List<AnimalEntry>(); float total = 0f;
            foreach (var a in animals)
            {
                if (a == null || a.prefab == null) continue;
                if (a.nightOnly && !isNight) continue;
                allowed.Add(a); total += a.weight;
            }
            if (allowed.Count == 0 || total <= 0f) return null;

            float r = Random.value * total;
            foreach (var a in allowed) { r -= a.weight; if (r <= 0f) return a; }
            return allowed[allowed.Count - 1];
        }

        private bool FindSpawnPoint(out Vector3 result)
        {
            result = Vector3.zero;
            for (int i = 0; i < 6; i++)
            {
                float dist = Random.Range(spawnDistanceRange.x, spawnDistanceRange.y);
                Vector2 c = Random.insideUnitCircle.normalized * dist;
                Vector3 cand = player.position + new Vector3(c.x, 0f, c.y);
                if (Vector3.Distance(cand, player.position) < minPlayerDist) continue;
                if (NavMesh.SamplePosition(cand, out var hit, navSampleRadius, NavMesh.AllAreas))
                { result = hit.position; return true; }
            }
            return false;
        }
    }
}
