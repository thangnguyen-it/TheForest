using UnityEngine;

namespace Companion.Data
{
    [CreateAssetMenu(menuName = "Companion/Commands/Stay", fileName = "Cmd_Stay")]
    public class Cmd_Stay : CompanionCommandDefinitionSO
    {
        public StayMode mode = StayMode.Here;
        public override CompanionState TargetState =>
            mode == StayMode.Hidden ? CompanionState.StayingHidden : CompanionState.Idle;
    }
}