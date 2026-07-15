using UnityEngine;
using Companion.Data;

namespace Companion.Events
{
    // Payload: damage amount + source.
    [CreateAssetMenu(menuName = "Companion/Events/Companion Damaged", fileName = "Ch_CompanionDamaged")]
    public class CompanionDamagedChannelSO : EventChannelSO<float, DamageSource> { }
}