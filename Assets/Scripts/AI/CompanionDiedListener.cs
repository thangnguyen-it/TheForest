using UnityEngine;
using UnityEngine.Events;
using Companion.Events;

namespace Companion.Listeners
{
    public class CompanionDiedListener : MonoBehaviour
    {
        [Header("Event Channel to Listen To")]
        [SerializeField] private CompanionDiedChannelSO channel;

        [Header("Response Actions")]
        [SerializeField] private UnityEvent<int, bool> onCompanionDied;

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

        private void Respond(int companionId, bool killedByPlayer)
        {
            onCompanionDied?.Invoke(companionId, killedByPlayer);
        }
    }
}