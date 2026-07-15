using UnityEngine;

namespace Companion.Data
{
    [CreateAssetMenu(menuName = "Companion/Commands/TakeBreak", fileName = "Cmd_TakeBreak")]
    public class Cmd_TakeBreak : CompanionCommandDefinitionSO
    {
        public override CompanionState TargetState => CompanionState.Resting;
    }
}