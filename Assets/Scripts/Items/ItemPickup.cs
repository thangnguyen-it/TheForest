using UnityEngine;
using TheForest.Interaction;
using TheForest.Player;

namespace TheForest.Items
{
    /// <summary>
    /// Vật phẩm trong thế giới có thể nhặt. Gắn lên prefab pickup (có Collider).
    /// Nhìn vào -> prompt "Nhặt X"; nhấn E -> vào Inventory.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemData item;
        [SerializeField] private int amount = 1;

        // Thêm vào ngay trên GetPrompt()
        public void Configure(ItemData newItem, int newAmount)
        {
            item = newItem;
            amount = newAmount;
        }
        public string GetPrompt() => $"Nhặt {item.displayName}" + (amount > 1 ? $" x{amount}" : "");

        public bool CanInteract() => item != null;

        public void Interact(GameObject interactor)
        {
            var inventory = interactor.GetComponent<Inventory>();
            if (inventory == null) return;

            int leftover = inventory.Add(item, amount);
            if (leftover == 0)
            {
                Destroy(gameObject); // nhặt hết -> biến mất
            }
            else if (leftover < amount)
            {
                amount = leftover;   // nhặt được một phần, còn lại nằm dưới đất
            }
            else
            {
                Debug.Log("[Pickup] Túi đầy, không nhặt được.");
            }
        }

        public void OnFocus() { /* TODO: bật outline highlight */ }
        public void OnLoseFocus() { /* TODO: tắt outline */ }
    }
}
