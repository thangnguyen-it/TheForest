using UnityEngine;
using Companion.Data;

namespace Companion.Events
{
    // Payload: ResourceType + world position request origin.
    [CreateAssetMenu(menuName = "Companion/Events/Resource Request", fileName = "Ch_ResourceRequest")]
    public class ResourceRequestChannelSO : EventChannelSO<ResourceType, Vector3> { }
}