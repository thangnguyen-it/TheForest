using UnityEngine;
using TheForest.Interaction;
using TheForest.Player;

namespace TheForest.World
{
    /// <summary>
    /// Drinkable lake/river volume. Raw water can restore thirst but hurt the player unless purified elsewhere.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NaturalWaterSource : MonoBehaviour, IInteractable
    {
        [Header("Water")]
        [SerializeField] private float thirstRestore = 35f;
        [SerializeField] private bool dirtyWater = true;
        [SerializeField] private float dirtyWaterDamage = 4f;

        [Header("Season")]
        [SerializeField] private bool freezesInWinter = true;
        [SerializeField] private SeasonSystem seasons;

        [Header("Prompt")]
        [SerializeField] private string cleanPrompt = "[E] Drink fresh water";
        [SerializeField] private string dirtyPrompt = "[E] Drink dirty water";
        [SerializeField] private string frozenPrompt = "Water is frozen";

        public bool IsDirty => dirtyWater;
        public bool IsAvailable => !IsFrozen();

        private void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        private void Awake()
        {
            if (seasons == null) seasons = FindFirstObjectByType<SeasonSystem>();
        }

        public string GetPrompt()
        {
            if (IsFrozen()) return frozenPrompt;
            return dirtyWater ? dirtyPrompt : cleanPrompt;
        }

        public bool CanInteract()
        {
            return !IsFrozen();
        }

        public void Interact(GameObject interactor)
        {
            if (IsFrozen()) return;

            var stats = interactor.GetComponent<SurvivalStats>();
            if (stats == null) return;

            stats.Drink(thirstRestore);
            if (dirtyWater && dirtyWaterDamage > 0f)
                stats.ApplyDamage(dirtyWaterDamage);
        }

        public void OnFocus() { }
        public void OnLoseFocus() { }

        public bool TryCollectWater(out bool cleanWater)
        {
            cleanWater = !dirtyWater;
            return !IsFrozen();
        }

        private bool IsFrozen()
        {
            return freezesInWinter && seasons != null && seasons.IsWinter;
        }
    }
}
