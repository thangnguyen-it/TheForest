using System;
using UnityEngine;

namespace TheForest.World
{
    public enum WorldSeason
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }

    /// <summary>
    /// Lightweight season clock for the survival loop: temperature, wildlife and food scarcity.
    /// Driven by DayNightCycle days so sleeping naturally advances the world.
    /// </summary>
    public class SeasonSystem : MonoBehaviour
    {
        public static SeasonSystem Instance { get; private set; }

        [Header("Time")]
        [SerializeField] private DayNightCycle dayNight;
        [SerializeField, Min(1)] private int daysPerSeason = 10;
        [SerializeField] private WorldSeason startSeason = WorldSeason.Spring;

        [Header("Temperature offset")]
        [SerializeField] private float springTemperatureOffset = 0f;
        [SerializeField] private float summerTemperatureOffset = 5f;
        [SerializeField] private float autumnTemperatureOffset = -2f;
        [SerializeField] private float winterTemperatureOffset = -12f;

        [Header("Resource availability")]
        [SerializeField, Range(0f, 2f)] private float springFoodMultiplier = 1f;
        [SerializeField, Range(0f, 2f)] private float summerFoodMultiplier = 1.25f;
        [SerializeField, Range(0f, 2f)] private float autumnFoodMultiplier = 0.9f;
        [SerializeField, Range(0f, 2f)] private float winterFoodMultiplier = 0.25f;
        [SerializeField, Range(0f, 2f)] private float winterFishMultiplier = 0.45f;
        [SerializeField, Range(0f, 2f)] private float winterAnimalMultiplier = 0.55f;

        public WorldSeason CurrentSeason { get; private set; }
        public int SeasonDay { get; private set; } = 1;
        public int DaysPerSeason => daysPerSeason;
        public float TemperatureOffset => GetTemperatureOffset(CurrentSeason);
        public float FoodMultiplier => GetFoodMultiplier(CurrentSeason);
        public float FishMultiplier => CurrentSeason == WorldSeason.Winter ? winterFishMultiplier : FoodMultiplier;
        public float AnimalMultiplier => CurrentSeason == WorldSeason.Winter ? winterAnimalMultiplier : FoodMultiplier;
        public bool IsWinter => CurrentSeason == WorldSeason.Winter;

        public event Action<WorldSeason> OnSeasonChanged;
        public event Action<int> OnSeasonDayChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (dayNight == null) dayNight = FindFirstObjectByType<DayNightCycle>();
            Recalculate(dayNight != null ? dayNight.DayNumber : 1, true);
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

        private void HandleNewDay(int dayNumber)
        {
            Recalculate(dayNumber, false);
        }

        public void ForceSeason(WorldSeason season, int seasonDay = 1)
        {
            CurrentSeason = season;
            SeasonDay = Mathf.Clamp(seasonDay, 1, daysPerSeason);
            OnSeasonChanged?.Invoke(CurrentSeason);
            OnSeasonDayChanged?.Invoke(SeasonDay);
        }

        private void Recalculate(int worldDay, bool initial)
        {
            int totalSeasonOffset = Mathf.Max(0, (int)startSeason);
            int zeroBasedDay = Mathf.Max(0, worldDay - 1);
            int seasonIndex = (totalSeasonOffset + zeroBasedDay / daysPerSeason) % 4;
            var nextSeason = (WorldSeason)seasonIndex;
            int nextSeasonDay = zeroBasedDay % daysPerSeason + 1;

            bool changed = nextSeason != CurrentSeason;
            CurrentSeason = nextSeason;
            SeasonDay = nextSeasonDay;

            if (initial || changed) OnSeasonChanged?.Invoke(CurrentSeason);
            OnSeasonDayChanged?.Invoke(SeasonDay);
        }

        private float GetTemperatureOffset(WorldSeason season)
        {
            switch (season)
            {
                case WorldSeason.Summer: return summerTemperatureOffset;
                case WorldSeason.Autumn: return autumnTemperatureOffset;
                case WorldSeason.Winter: return winterTemperatureOffset;
                default: return springTemperatureOffset;
            }
        }

        private float GetFoodMultiplier(WorldSeason season)
        {
            switch (season)
            {
                case WorldSeason.Summer: return summerFoodMultiplier;
                case WorldSeason.Autumn: return autumnFoodMultiplier;
                case WorldSeason.Winter: return winterFoodMultiplier;
                default: return springFoodMultiplier;
            }
        }
    }
}
