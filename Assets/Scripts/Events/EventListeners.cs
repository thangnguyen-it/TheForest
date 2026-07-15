using UnityEngine;
using UnityEngine.Events;
using Companion.Data;

namespace Companion.Events
{
    public class VoidEventListener : MonoBehaviour
    {
        [SerializeField] private VoidEventChannelSO channel;
        [SerializeField] private UnityEvent response;
        private void OnEnable() { if (channel) channel.Register(OnRaised); }
        private void OnDisable() { if (channel) channel.Unregister(OnRaised); }
        private void OnRaised() => response?.Invoke();
    }

}