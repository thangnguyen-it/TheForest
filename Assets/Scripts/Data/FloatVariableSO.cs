using UnityEngine;

namespace Companion.Data
{
    /// <summary>
    /// Shared runtime float read by multiple systems WITHOUT cross-system polling of fields.
    /// e.g. CompanionEnergy, DetectionRadiusMultiplier.
    /// Resets to initialValue on enable in the editor to avoid persisted-asset drift.
    /// </summary>
    [CreateAssetMenu(menuName = "Companion/Data/Float Variable", fileName = "Var_New")]
    public class FloatVariableSO : ScriptableObject
    {
        [SerializeField] private float initialValue;
        [System.NonSerialized] private float runtimeValue;

        public float Value
        {
            get => runtimeValue;
            set => runtimeValue = value;
        }

        public void SetValue(float v) => runtimeValue = v;
        public void ApplyDelta(float d) => runtimeValue += d;

        private void OnEnable() => runtimeValue = initialValue;
        private void OnDisable() => runtimeValue = initialValue;
    }
}
