using UnityEngine;

namespace Companion.Data
{
    [CreateAssetMenu(menuName = "Companion/Commands/Clear", fileName = "Cmd_Clear")]
    public class Cmd_Clear : CompanionCommandDefinitionSO
    {
        [Tooltip("Allowed radii: 5, 10, 20.")]
        public float radius = 5f;
        public override CompanionState TargetState => CompanionState.Clearing;
    }
}