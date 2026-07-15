using UnityEngine;
using TheForest.Interaction;
using TheForest.Player;

namespace TheForest.Items
{
    /// <summary>
    /// World item that can be consumed immediately (berries, oysters, meds) or picked up.
    /// Uses ItemData restore values so inventory and world pickups stay consistent.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WorldConsumable : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemData item;
        [SerializeField, Min(1)] private int amount = 1;
        [SerializeField] private bool consumeImmediately = true;
        [SerializeField] private bool destroyWhenEmpty = true;

        [Header("Prompt")]
        [SerializeField] private string consumeVerb = "Eat";
        [SerializeField] private string pickupVerb = "Pick up";

        public void Configure(ItemData newItem, int newAmount, bool immediate)
        {
            item = newItem;
            amount = Mathf.Max(1, newAmount);
            consumeImmediately = immediate;
        }

        public string GetPrompt()
        {
            if (item == null) return string.Empty;
            string verb = consumeImmediately ? consumeVerb : pickupVerb;
            string suffix = amount > 1 ? $" x{amount}" : string.Empty;
            return $"[E] {verb} {item.displayName}{suffix}";
        }

        public bool CanInteract()
        {
            return item != null && amount > 0;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract()) return;

            if (consumeImmediately)
            {
                var stats = interactor.GetComponent<SurvivalStats>();
                if (stats == null) return;

                stats.ConsumeItem(item);
                ConsumeOneUnit();
                return;
            }

            var inventory = interactor.GetComponent<Inventory>();
            if (inventory == null) return;

            int leftover = inventory.Add(item, amount);
            if (leftover == 0)
            {
                amount = 0;
                FinishIfEmpty();
            }
            else
            {
                amount = leftover;
            }
        }

        public void OnFocus() { }
        public void OnLoseFocus() { }

        private void ConsumeOneUnit()
        {
            amount--;
            FinishIfEmpty();
        }

        private void FinishIfEmpty()
        {
            if (amount > 0 || !destroyWhenEmpty) return;
            Destroy(gameObject);
        }
    }
}
