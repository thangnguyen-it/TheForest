using UnityEngine;

namespace Companion.Data
{
    [CreateAssetMenu(menuName = "Companion/Data/Emote", fileName = "Emote_New")]
    public class EmoteDefinitionSO : ScriptableObject
    {
        public string emoteName;
        public AnimationClip clip;
        [Range(0f, 1f)] public float layerWeight = 1f;
        [Tooltip("TRUE only for Nod.")]
        public bool isReciprocal;
    }
}