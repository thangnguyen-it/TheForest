using UnityEngine;
using Companion.Data;
using Companion.Events;

namespace Companion.FSM
{
    /// <summary>
    /// On StayingHidden, writes detectionRadiusMultiplier to a FloatVariableSO that the
    /// hostile-AI perception system reads independently (§9). No direct enemy-AI calls.
    /// </summary>
    public class StealthModifierApplier : MonoBehaviour
    {
        [SerializeField] private CompanionStateChannelSO stateChannel;
        [SerializeField] private FloatVariableSO detectionRadiusMultiplier;
        [SerializeField, Range(0.1f, 1f)] private float hiddenMultiplier = 0.5f;
        [SerializeField] private float normalMultiplier = 1f;

        private void OnEnable() { if (stateChannel) stateChannel.Register(OnState); }
        private void OnDisable() { if (stateChannel) stateChannel.Unregister(OnState); }

        private void OnState(CompanionState s)
        {
            if (detectionRadiusMultiplier == null) return;
            detectionRadiusMultiplier.SetValue(s == CompanionState.StayingHidden ? hiddenMultiplier : normalMultiplier);
        }
    }
}
