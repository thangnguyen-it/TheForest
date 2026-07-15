using System;
using UnityEngine;

namespace Companion.Events
{
    /// <summary>Parameterless channel.</summary>
    [CreateAssetMenu(menuName = "Companion/Events/Void Channel", fileName = "VoidEvent")]
    public class VoidEventChannelSO : ScriptableObject
    {
        private event Action OnRaised;
        public void Raise() => OnRaised?.Invoke();
        public void Register(Action c) => OnRaised += c;
        public void Unregister(Action c) => OnRaised -= c;
    }

    /// <summary>Generic single-payload channel base.</summary>
    public abstract class EventChannelSO<T> : ScriptableObject
    {
        private event Action<T> OnRaised;
        public void Raise(T payload) => OnRaised?.Invoke(payload);
        public void Register(Action<T> c) => OnRaised += c;
        public void Unregister(Action<T> c) => OnRaised -= c;
    }

    /// <summary>Generic two-payload channel base.</summary>
    public abstract class EventChannelSO<T1, T2> : ScriptableObject
    {
        private event Action<T1, T2> OnRaised;
        public void Raise(T1 a, T2 b) => OnRaised?.Invoke(a, b);
        public void Register(Action<T1, T2> c) => OnRaised += c;
        public void Unregister(Action<T1, T2> c) => OnRaised -= c;
    }
}
