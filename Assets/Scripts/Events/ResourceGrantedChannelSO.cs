using UnityEngine;
using Companion.Data;

namespace Companion.Events
{
    // Payload: ResourceType + granted amount.
    [CreateAssetMenu(menuName = "Companion/Events/Resource Granted", fileName = "Ch_ResourceGranted")]
    public class ResourceGrantedChannelSO : EventChannelSO<ResourceType, int> { }
}