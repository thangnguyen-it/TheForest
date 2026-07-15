namespace Companion.Data
{
    public enum ResourceType { Sticks, Rocks, Logs, Fish, Berries, Arrows, Radio }

    public enum DeliveryMode { DropHere, BringToMe, FollowMe, FillHolder }

    public enum BuildAction { Fire, Shelter, FinishStructure, RepairStructure, ClearShelter }

    public enum StayMode { Here, Hidden }

    public enum DamageSource { Player, HostileAI, Environment, Unknown }

    public enum CompanionState
    {
        Idle, Following, GatheringResource, Building, Clearing,
        Resting, StayingHidden, Fleeing, KnockedDown, Dead
    }
}
