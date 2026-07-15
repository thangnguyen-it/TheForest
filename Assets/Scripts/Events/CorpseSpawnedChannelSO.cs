using UnityEngine;

namespace Companion.Events
{
    /// <summary>Phát khi một xác vừa xuất hiện. Người ăn xác/khiêng xác subscribe.</summary>
    [CreateAssetMenu(menuName = "Companion/Events/Corpse Spawned", fileName = "Ch_CorpseSpawned")]
    public class CorpseSpawnedChannelSO : EventChannelSO<Transform> { }
}
