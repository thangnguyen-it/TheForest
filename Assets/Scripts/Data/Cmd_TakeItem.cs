using UnityEngine;

namespace Companion.Data
{
    [CreateAssetMenu(menuName = "Companion/Commands/TakeItem", fileName = "Cmd_TakeItem")]
    public class Cmd_TakeItem : CompanionCommandDefinitionSO
    {
        [Tooltip("Cosmetic visual layer index — NEVER modifies any stat.")]
        public int cosmeticLayerId;
        // Returns current state so issuing this command does not change FSM state.
        public override CompanionState TargetState => CompanionState.Idle;
        public bool isCosmeticOnly => true;
    }
}