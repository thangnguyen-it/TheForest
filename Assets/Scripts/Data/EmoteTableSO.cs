using UnityEngine;

namespace Companion.Data
{
    [CreateAssetMenu(menuName = "Companion/Data/Emote Table", fileName = "EmoteTable")]
    public class EmoteTableSO : ScriptableObject
    {
        public EmoteDefinitionSO[] emotes;

        public EmoteDefinitionSO Find(string emoteName)
        {
            if (emotes == null) return null;
            for (int i = 0; i < emotes.Length; i++)
                if (emotes[i] != null && emotes[i].emoteName == emoteName)
                    return emotes[i];
            return null;
        }
    }
}