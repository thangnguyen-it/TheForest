using UnityEngine;

namespace TheForest.AI
{
    /// <summary>
    /// Hiệu ứng trạng thái lên động vật: poison DoT + slow (cho poison arrow).
    /// GDD: poison arrow làm Deer chậm lại/dừng -> dễ tiếp cận.
    /// </summary>
    [RequireComponent(typeof(AnimalHealth))]
    public class AnimalStatus : MonoBehaviour
    {
        private AnimalHealth _health;
        private AnimalAI _ai;

        private float _poisonTimer;
        private float _poisonDps;
        private float _slowUntil;
        private float _slowFactor = 1f;

        public float SpeedMultiplier => Time.time < _slowUntil ? _slowFactor : 1f;

        private void Awake()
        {
            _health = GetComponent<AnimalHealth>();
            _ai = GetComponent<AnimalAI>();
        }

        private void Update()
        {
            if (_poisonTimer > 0f)
            {
                _poisonTimer -= Time.deltaTime;
                _health.ApplyChop(_poisonDps * Time.deltaTime, null); // DoT (null attacker)
            }
        }

        public void ApplyPoisonAndSlow(float dps, float duration, float slowFactor)
        {
            _poisonDps = dps;
            _poisonTimer = Mathf.Max(_poisonTimer, duration);
            _slowFactor = Mathf.Clamp01(slowFactor);
            _slowUntil = Time.time + duration;
        }
    }
}
