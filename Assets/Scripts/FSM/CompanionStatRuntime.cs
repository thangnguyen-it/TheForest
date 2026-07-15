using UnityEngine;
using Companion.Data;

namespace Companion.FSM
{
    /// <summary>
    /// Runtime wrapper around a stat definition. Holds the live value in a FloatVariableSO
    /// so other systems read it via the SO, not by polling a field.
    /// </summary>
    [System.Serializable]
    public class CompanionStatRuntime
    {
        public CompanionStatDefinitionSO definition;
        public FloatVariableSO sharedVariable; // optional shared mirror

        private float _value;
        private float _decayAccum;

        public float Value => _value;

        public void Init()
        {
            _value = definition != null ? definition.baseValue : 0f;
            Mirror();
        }

        public void Add(float delta)
        {
            if (definition == null) return;
            _value = definition.Clamp(_value + delta);
            Mirror();
        }

        public void SetValue(float v)
        {
            if (definition == null) return;
            _value = definition.Clamp(v);
            Mirror();
        }

        /// <summary>Time-based decay tick. Call every frame; internal accumulator gates the tick.</summary>
        public void TickDecay(float dt)
        {
            if (definition == null || definition.decayTickInterval <= 0f) return;
            _decayAccum += dt;
            while (_decayAccum >= definition.decayTickInterval)
            {
                _decayAccum -= definition.decayTickInterval;
                Add(-definition.decayRatePerTick);
            }
        }

        private void Mirror()
        {
            if (sharedVariable != null) sharedVariable.SetValue(_value);
        }
    }
}
