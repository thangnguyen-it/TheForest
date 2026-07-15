using UnityEngine;
using TheForest.AI;
using Companion.Events;

namespace Companion.Events
{
    public class EventBusBootstrapper : MonoBehaviour
    {
        [SerializeField] private NoiseEventChannelSO noiseChannel;

        private void Awake()
        {
            NoiseSystem.SetChannel(noiseChannel);
        }
    }
}