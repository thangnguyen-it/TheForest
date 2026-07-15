using UnityEngine;
using UnityEngine.InputSystem;
using TheForest.Items;

namespace TheForest.Player
{
    /// <summary>
    /// Bản đồ phím nóng điều hướng trang bị nhanh bằng phím số.
    /// </summary>
    public class EquipHotkeys : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private EquipmentController equipment;
        [SerializeField] private ItemData[] hotbar = new ItemData[3];

        private void Awake()
        {
            if (equipment == null) equipment = GetComponent<EquipmentController>();
        }

        public void OnEquip1(InputValue value) { if (value.isPressed) TryEquip(0); }
        public void OnEquip2(InputValue value) { if (value.isPressed) TryEquip(1); }
        public void OnEquip3(InputValue value) { if (value.isPressed) TryEquip(2); }

        private void TryEquip(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= hotbar.Length || hotbar[slotIndex] == null) return;
            equipment.Equip(hotbar[slotIndex]);
        }
    }
}