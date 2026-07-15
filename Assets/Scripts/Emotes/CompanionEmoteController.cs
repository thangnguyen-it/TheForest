using UnityEngine;
using Companion.Data;
using Companion.Events;

namespace Companion.Emotes
{
    /// <summary>
    /// Drives an ADDITIVE animator layer. Zero references to the FSM.
    /// Listens to a Void channel (player nod) and plays the reciprocal Nod emote.
    /// Emotes NEVER modify any stat.
    /// </summary>
    public class CompanionEmoteController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private EmoteTableSO emoteTable;
        [SerializeField] private int additiveLayerIndex = 1;
        [SerializeField] private VoidEventChannelSO playerNodChannel;

        private void OnEnable() { if (playerNodChannel) playerNodChannel.Register(OnPlayerNod); }
        private void OnDisable() { if (playerNodChannel) playerNodChannel.Unregister(OnPlayerNod); }

        private void OnPlayerNod()
        {
            var nod = emoteTable != null ? emoteTable.Find("Nod") : null;
            if (nod != null && nod.isReciprocal) Play(nod);
        }

        public void Play(EmoteDefinitionSO emote)
        {
            if (emote == null || animator == null || emote.clip == null) return;
            animator.SetLayerWeight(additiveLayerIndex, emote.layerWeight); // additive — never blocks locomotion
            animator.CrossFade(emote.clip.name, 0.1f, additiveLayerIndex);
        }
    }
}
