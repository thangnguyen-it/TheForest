using UnityEngine;

namespace Companion.Data
{
    [CreateAssetMenu(menuName = "Companion/Commands/FollowMe", fileName = "Cmd_FollowMe")]
    public class Cmd_FollowMe : CompanionCommandDefinitionSO
    {
        public override CompanionState TargetState => CompanionState.Following;
    }
}