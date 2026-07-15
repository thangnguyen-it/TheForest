using UnityEngine;
using Companion.Data;
using Companion.Events;

namespace Companion.FSM
{
    /// <summary>
    /// Receives raw player input commands and forwards the chosen command SO over a channel.
    /// PlayerInput → (this) → CommandIssuedChannelSO → FSM. No direct FSM reference.
    /// </summary>
    public class CompanionCommandRouter : MonoBehaviour
    {
        [SerializeField] private CommandIssuedChannelSO commandIssued;

        /// <summary>Wire to UI buttons / radial menu entries (each carries a command asset).</summary>
        public void IssueCommand(CompanionCommandDefinitionSO command)
        {
            if (command == null) return;
            commandIssued?.Raise(command);
        }
    }
}
