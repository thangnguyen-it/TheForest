using UnityEngine;
using Companion.Data;

namespace Companion.Events
{
    // Command channel: carries the command definition asset.
    [CreateAssetMenu(menuName = "Companion/Events/Command Issued", fileName = "Ch_CommandIssued")]
    public class CommandIssuedChannelSO : EventChannelSO<CompanionCommandDefinitionSO> { }
}