// ═══════════════════════════════════════════════════════════════════════════════
// 2.9  ELECTRICITY CONFIG
// ═══════════════════════════════════════════════════════════════════════════════
using UnityEngine;

namespace TheForest.Building.Config
{
    [CreateAssetMenu(fileName = "ElectricityConfig", menuName = "The Forest/Building/Electricity Config")]
    public class ElectricityConfig : ScriptableObject
    {
        [Header("Solar Panel")]
        public float solarOutputWatts = 10f;
        public float solarStartHour = 6f;
        public float solarEndHour = 19f;

        [Header("Battery (Large)")]
        public float batteryCapacityWh = 50f;   // watt-hours stored
        public float batteryChargeRateW = 8f;    // watts charged/sec
        public float batteryDischargeRateW = 5f;    // watts drawn/sec at night

        [Header("Wire")]
        public float wireConnectionRadius = 0.50f;
        public float minPowerThreshold = 0.10f; // below this = off

        [Header("Consumer Costs (watts)")]
        public float lightBulbW = 2f;
        public float spotlightW = 8f;
        public float multiTrapW = 5f;
        public float poweredCrossW = 3f;
        public float spinTrapW = 4f;
        public float boneTowerW = 6f;
    }
}