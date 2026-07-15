using System;
using Companion.Data;

namespace Companion.FSM
{
    [Serializable]
    public class CompanionSaveData
    {
        public int typeId;
        public CompanionState state;   // Dead is terminal serialized value
        public float health;
        public float energy, sentiment, memory, fear;
        public bool isDead;
        public bool playerKilled;
        public int killedOnDay;      // -1 if alive
    }
}
