using UnityEngine;

namespace Companion.Data
{
    /// <summary>
    /// Base command definition. The FSM dispatches purely on the command's
    /// abstract intent — adding/removing a command must require NO FSM code change.
    /// </summary>
    public abstract class CompanionCommandDefinitionSO : ScriptableObject
    {
        [Header("Common")]
        public string commandId;
        [TextArea] public string description;

        /// <summary>The FSM state this command requests on issue.</summary>
        public abstract CompanionState TargetState { get; }
    }
}