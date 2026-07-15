using UnityEngine;
using Companion.Events;

namespace Companion.Emotes
{
    /// <summary>
    /// Detects player nod within a time/distance window and raises a VoidEventChannelSO.
    /// Independent of the companion FSM and emote controller (decoupled via channel).
    /// </summary>
    public class PlayerGestureListener : MonoBehaviour
    {
        [SerializeField] private VoidEventChannelSO playerNodChannel;
        [SerializeField] private Transform companion;
        [SerializeField] private float maxDistance = 5f;

        /// <summary>Call from input system when a nod gesture is recognized.</summary>
        public void OnNodGestureRecognized()
        {
            if (companion == null) { playerNodChannel?.Raise(); return; }
            if (Vector3.Distance(transform.position, companion.position) <= maxDistance)
                playerNodChannel?.Raise();
        }
    }
}
