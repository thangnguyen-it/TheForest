using UnityEngine;

namespace Companion.Data
{
    /// <summary>
    /// Pure tunable data for a single companion stat (Energy, Sentiment, Memory, Fear).
    /// Contains NO runtime state — runtime value lives in a FloatVariableSO.
    /// </summary>
    [CreateAssetMenu(menuName = "Companion/Data/Stat Definition", fileName = "Stat_New")]
    public class CompanionStatDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string statName = "Energy";

        [Header("Values")]
        public float baseValue = 100f;
        public float minValue = 0f;
        public float maxValue = 100f;

        [Header("Decay")]
        [Tooltip("Amount removed per decay tick.")]
        public float decayRatePerTick = 0.01f;
        [Tooltip("Seconds between decay ticks.")]
        public float decayTickInterval = 3f;

        [Header("Regen")]
        [Tooltip("Amount restored per second via Rest / Bench.")]
        public float regenRatePerSecond = 5f;

        public float Clamp(float v) => Mathf.Clamp(v, minValue, maxValue);
    }
}
