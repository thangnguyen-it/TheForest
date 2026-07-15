using UnityEngine;

namespace TheForest.World
{
    /// <summary>
    /// Enables or disables a resource node based on the current season.
    /// Useful for berries, oysters, herbs and small forage prefabs.
    /// </summary>
    public class SeasonalResource : MonoBehaviour
    {
        [SerializeField] private SeasonSystem seasons;
        [SerializeField] private bool availableInWinter = false;
        [SerializeField] private bool useSeasonFoodMultiplier = true;
        [SerializeField, Range(0f, 1f)] private float baseAvailability = 1f;
        [SerializeField] private GameObject[] controlledObjects;
        [SerializeField] private Collider[] controlledColliders;

        private int _stableSeed;

        private void Awake()
        {
            if (seasons == null) seasons = FindFirstObjectByType<SeasonSystem>();
            _stableSeed = Mathf.Abs(HashCode(transform.position, gameObject.name));
        }

        private void OnEnable()
        {
            if (seasons != null) seasons.OnSeasonChanged += HandleSeasonChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (seasons != null) seasons.OnSeasonChanged -= HandleSeasonChanged;
        }

        private void HandleSeasonChanged(WorldSeason season)
        {
            Refresh();
        }

        public void Refresh()
        {
            bool available = ComputeAvailable();
            Apply(available);
        }

        private bool ComputeAvailable()
        {
            if (seasons == null) return true;
            if (seasons.IsWinter && !availableInWinter) return false;

            float chance = baseAvailability;
            if (useSeasonFoodMultiplier) chance *= seasons.FoodMultiplier;
            chance = Mathf.Clamp01(chance);

            float roll = StableSeasonRoll(seasons.CurrentSeason);
            return roll <= chance;
        }

        private void Apply(bool available)
        {
            if (controlledObjects != null && controlledObjects.Length > 0)
            {
                foreach (var obj in controlledObjects)
                    if (obj != null) obj.SetActive(available);
            }
            else
            {
                for (int i = 0; i < transform.childCount; i++)
                    transform.GetChild(i).gameObject.SetActive(available);
            }

            if (controlledColliders != null)
            {
                foreach (var c in controlledColliders)
                    if (c != null) c.enabled = available;
            }
        }

        private float StableSeasonRoll(WorldSeason season)
        {
            int n = _stableSeed ^ ((int)season * 73856093);
            n = (n << 13) ^ n;
            int hashed = n * (n * n * 15731 + 789221) + 1376312589;
            return 1f - (hashed & 0x7fffffff) / 1073741824f * 0.5f;
        }

        private static int HashCode(Vector3 position, string objectName)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Mathf.RoundToInt(position.x * 10f);
                hash = hash * 31 + Mathf.RoundToInt(position.y * 10f);
                hash = hash * 31 + Mathf.RoundToInt(position.z * 10f);
                hash = hash * 31 + (objectName != null ? objectName.GetHashCode() : 0);
                return hash;
            }
        }
    }
}
