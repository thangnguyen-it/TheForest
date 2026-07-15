using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TheForest.Player;
using TheForest.Items;

namespace TheForest.UI
{
    /// <summary>
    /// Một ô slot trong lưới túi đồ: icon + số lượng.
    /// Kế thừa IPointerClickHandler để nhận diện thao tác click chuột phải đưa đồ vào khu chế.
    /// </summary>
    public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMPro.TextMeshProUGUI countText;
        [SerializeField] private GameObject emptyOverlay;

        public System.Action<ItemData> OnRightClick;
        private ItemData _item;

        public void Refresh(InventorySlot slot)
        {
            bool empty = slot == null || slot.IsEmpty;
            _item = empty ? null : slot.item;

            if (iconImage != null)
            {
                iconImage.enabled = !empty;
                if (!empty) iconImage.sprite = slot.item.icon;
            }

            if (countText != null)
                countText.text = (!empty && slot.count > 1) ? slot.count.ToString() : string.Empty;

            if (emptyOverlay != null)
                emptyOverlay.SetActive(empty);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && _item != null)
            {
                OnRightClick?.Invoke(_item);
            }
        }
    }
}