using UnityEngine;

namespace Companion.Events
{
    /// <summary>Yêu cầu thay đổi aggression cục bộ theo zone. AggressionManager subscribe & xử lý.</summary>
    public readonly struct AggressionDelta
    {
        public readonly int ZoneId;
        public readonly float Amount; // dương = tăng, âm = giảm

        public AggressionDelta(int zoneId, float amount)
        {
            ZoneId = zoneId;
            Amount = amount;
        }
    }

    [CreateAssetMenu(menuName = "Companion/Events/Aggression", fileName = "Ch_Aggression")]
    public class AggressionEventChannelSO : EventChannelSO<AggressionDelta> { }
}
