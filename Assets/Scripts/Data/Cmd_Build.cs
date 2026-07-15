using UnityEngine;

namespace Companion.Data
{
    [CreateAssetMenu(menuName = "Companion/Commands/Build", fileName = "Cmd_Build")]
    public class Cmd_Build : CompanionCommandDefinitionSO
    {
        public BuildAction action = BuildAction.Fire;
        [Tooltip("FinishStructure must continue until resources run out — handled in Building state.")]
        public bool continueUntilResourcesExhausted = true;
        public override CompanionState TargetState => CompanionState.Building;
    }
}