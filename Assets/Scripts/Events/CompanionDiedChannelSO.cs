using UnityEngine;

namespace Companion.Events
{
    // Payload: companionId + killedByPlayer.
    [CreateAssetMenu(menuName = "Companion/Events/Companion Died", fileName = "Ch_CompanionDied")]
    public class CompanionDiedChannelSO : EventChannelSO<int, bool> { }
}