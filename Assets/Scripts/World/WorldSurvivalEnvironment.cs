using UnityEngine;
using TheForest.Player;

namespace TheForest.World
{
    /// <summary>
    /// Converts world state into player survival inputs: ambient temperature and wetness.
    /// This is intentionally scene-driven so designers can tune cold, rain and fire warmth per map.
    /// </summary>
    public class WorldSurvivalEnvironment : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SurvivalStats stats;
        [SerializeField] private DayNightCycle dayNight;
        [SerializeField] private SeasonSystem seasons;

        [Header("Ambient temperature")]
        [SerializeField] private float baseDayTemperature = 18f;
        [SerializeField] private float nightTemperaturePenalty = 7f;
        [SerializeField] private float rainTemperaturePenalty = 4f;
        [SerializeField] private AnimationCurve dayWarmthCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Exposure")]
        [Tooltip("Layers that count as roof, cave ceiling or dense shelter above the player.")]
        [SerializeField] private LayerMask shelterMask = 0;
        [SerializeField] private float overheadCheckDistance = 12f;
        [SerializeField] private float overheadCheckRadius = 0.35f;

        [Header("Fire warmth")]
        [SerializeField] private float fireWarmthRadius = 6f;
        [SerializeField] private float fireTemperatureBonus = 14f;
        [SerializeField] private bool fireDriesPlayer = true;

        public bool IsSheltered { get; private set; }
        public bool IsNearFire { get; private set; }
        public float AmbientTemperature { get; private set; }

        private void Awake()
        {
            if (stats == null) stats = GetComponent<SurvivalStats>();
            if (stats == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) stats = player.GetComponent<SurvivalStats>();
            }

            if (dayNight == null) dayNight = FindFirstObjectByType<DayNightCycle>();
            if (seasons == null) seasons = FindFirstObjectByType<SeasonSystem>();
        }

        private void Update()
        {
            EnsureStats();
            if (stats == null) return;

            IsSheltered = CheckSheltered();
            IsNearFire = CheckNearFire(out float fireFactor);

            bool raining = WeatherSystem.Instance != null && WeatherSystem.Instance.IsRaining;
            bool wet = raining && !IsSheltered;
            if (wet && fireDriesPlayer && IsNearFire) wet = false;
            stats.SetWet(wet);

            AmbientTemperature = ComputeAmbientTemperature(raining, fireFactor);
            stats.SetTemperature(AmbientTemperature);
        }

        private void EnsureStats()
        {
            if (stats != null) return;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) stats = player.GetComponent<SurvivalStats>();
        }

        private float ComputeAmbientTemperature(bool raining, float fireFactor)
        {
            float dayFactor = 1f;
            if (dayNight != null)
            {
                float normalizedHour = Mathf.Repeat(dayNight.CurrentHour, 24f) / 24f;
                float solarFactor = (Mathf.Sin(normalizedHour * Mathf.PI * 2f - Mathf.PI / 2f) + 1f) * 0.5f;
                dayFactor = dayWarmthCurve.Evaluate(solarFactor);
            }

            float temperature = baseDayTemperature - nightTemperaturePenalty * (1f - dayFactor);
            if (raining && !IsSheltered) temperature -= rainTemperaturePenalty;
            if (seasons != null) temperature += seasons.TemperatureOffset;
            temperature += fireTemperatureBonus * fireFactor;
            return temperature;
        }

        private bool CheckSheltered()
        {
            if (shelterMask.value == 0) return false;

            Vector3 origin = stats.transform.position + Vector3.up * 0.2f;
            return Physics.SphereCast(
                origin,
                overheadCheckRadius,
                Vector3.up,
                out _,
                overheadCheckDistance,
                shelterMask,
                QueryTriggerInteraction.Ignore);
        }

        private bool CheckNearFire(out float fireFactor)
        {
            fireFactor = 0f;
            var fires = FireRegistry.Fires;
            for (int i = fires.Count - 1; i >= 0; i--)
            {
                var fire = fires[i];
                if (fire == null || !fire.IsBurning) continue;

                float distance = Vector3.Distance(stats.transform.position, fire.Position);
                if (distance > fireWarmthRadius) continue;

                fireFactor = Mathf.Max(fireFactor, 1f - distance / fireWarmthRadius);
            }

            return fireFactor > 0f;
        }
    }
}
