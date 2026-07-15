using UnityEngine;
using UnityEngine.Events;
using Companion.Data;
using Companion.Events;

namespace Companion.Listeners
{
    public class CompanionStateListener : MonoBehaviour
    {
        [Header("Event Channel to Listen To")]
        [SerializeField] private CompanionStateChannelSO channel;

        [Header("Response Actions")]
        [SerializeField] private UnityEvent<CompanionState> onStateChanged;

        private void OnEnable()
        {
            if (channel != null)
                channel.Register(Respond);
        }

        private void OnDisable()
        {
            if (channel != null)
                channel.Unregister(Respond);
        }

        private void Respond(CompanionState state)
        {
            onStateChanged?.Invoke(state);
        }
    }
}