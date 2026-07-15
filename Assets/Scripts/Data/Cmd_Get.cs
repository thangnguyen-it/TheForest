using UnityEngine;

namespace Companion.Data
{
    [CreateAssetMenu(menuName = "Companion/Commands/Get", fileName = "Cmd_Get")]
    public class Cmd_Get : CompanionCommandDefinitionSO
    {
        public ResourceType resource = ResourceType.Sticks;
        public DeliveryMode delivery = DeliveryMode.DropHere;
        public override CompanionState TargetState => CompanionState.GatheringResource;
    }
}