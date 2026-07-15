using UnityEngine;
using System;

namespace Companion.Events
{
    [CreateAssetMenu(menuName = "Companion/Events/Void Channel", fileName = "Ch_PlayerNod")]
    public class PlayerNodChannelSO : ScriptableObject
    {
        public Action OnEventRaised;

        public void RaiseEvent()
        {
            OnEventRaised?.Invoke();
        }
    }
}