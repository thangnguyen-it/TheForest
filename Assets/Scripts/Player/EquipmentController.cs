using System;
using UnityEngine;
using TheForest.Items;

namespace TheForest.Player
{
    /// <summary>
    /// Quản lý vũ khí/công cụ ĐANG CẦM trên tay người chơi.
    /// Spawn model vào "hand anchor", expose item đang cầm cho combat/chặt cây.
    /// Cập nhật và đồng bộ trực tiếp thực thể model tới WeaponSwinger và WeaponPoseController.
    ///
    /// FIX (Phần A.4): trước đây có gọi `GetComponent&lt;WeaponSwinger&gt;()?.SetWeaponVisual(...)`
    /// nhưng thân hàm đó ở WeaponSwinger hoàn toàn RỖNG — đã xóa lời gọi (WeaponSwinger tự đọc
    /// WeaponModel/WeaponAnimator từ đây mỗi khi cần, không cần được "đẩy" qua lời gọi riêng).
    ///
    /// GIAI ĐOẠN 3: GetChopDamage() giờ đọc field `damage` kế thừa từ MeleeWeaponItemData (trước đây
    /// là `chopDamage` khai báo riêng trên AxeItemData) — xem AxeItemData.cs bản cập nhật.
    /// </summary>
    public class EquipmentController : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [Tooltip("Điểm gắn model vũ khí (con của Camera, lệch phải-dưới như tay FPS).")]
        [SerializeField] private Transform handAnchor;
        [SerializeField] private Inventory inventory;

        // Item đang cầm (null = tay không)
        public ItemData EquippedItem { get; private set; }
        public bool HasEquipped => EquippedItem != null;

        // Các thuộc tính phơi ra cho WeaponSwinger đọc dữ liệu trực quan
        public Transform WeaponModel { get; private set; }
        public Animator WeaponAnimator { get; private set; }

        private GameObject _spawnedModel;

        public event Action<ItemData> OnEquippedChanged; // cho HUD/animation

        private void Awake()
        {
            if (inventory == null) inventory = GetComponent<Inventory>();
        }

        /// <summary>Cầm một item (nếu equippable và còn trong túi).</summary>
        public void Equip(ItemData item)
        {
            if (item == null || !item.isEquippable) return;
            if (inventory != null && !inventory.Has(item, 1)) return; // không có trong túi

            // Đang cầm đúng item này rồi -> bỏ cầm (toggle)
            if (EquippedItem == item)
            {
                Unequip();
                return;
            }

            ClearModel();
            EquippedItem = item;

            if (item.equipPrefab != null && handAnchor != null)
            {
                _spawnedModel = Instantiate(item.equipPrefab, handAnchor);
                _spawnedModel.transform.localPosition = Vector3.zero;
                _spawnedModel.transform.localRotation = Quaternion.identity;
            }

            // Tự động trích xuất dữ liệu hình ảnh trực quan ngay khi spawn thành công
            SetWeaponVisual(_spawnedModel);

            // Cấp model động cho PoseController (WeaponSwinger tự đọc WeaponModel/WeaponAnimator khi cần)
            GetComponent<WeaponPoseController>()?.SetModel(_spawnedModel);

            OnEquippedChanged?.Invoke(EquippedItem);
        }

        public void Unequip()
        {
            ClearModel();
            EquippedItem = null;
            SetWeaponVisual(null); // Xóa liên kết hình ảnh khi cất vũ khí

            // Giải phóng liên kết model trên bộ điều khiển phòng thủ
            GetComponent<WeaponPoseController>()?.SetModel(null);

            OnEquippedChanged?.Invoke(null);
        }

        private void ClearModel()
        {
            if (_spawnedModel != null) Destroy(_spawnedModel);
            _spawnedModel = null;
        }

        /// <summary>
        /// Damage chặt cây của vũ khí đang cầm. 0 nếu không cầm rìu.
        /// </summary>
        public float GetChopDamage()
        {
            if (EquippedItem is AxeItemData axe) return axe.damage; // trước đây: axe.chopDamage
            return 0f;
        }

        /// <summary>
        /// Nếu vũ khí đang cầm đã bị tiêu hết khỏi túi thì tự bỏ cầm.
        /// </summary>
        public void ValidateEquipped()
        {
            if (EquippedItem != null && inventory != null && !inventory.Has(EquippedItem, 1))
                Unequip();
        }

        /// <summary>
        /// Sửa đổi và trích xuất thành phần Transform/Animator từ thực thể hình ảnh được truyền vào.
        /// </summary>
        public void SetWeaponVisual(GameObject model)
        {
            WeaponModel = model != null ? model.transform : null;
            WeaponAnimator = model != null ? model.GetComponentInChildren<Animator>() : null;
        }
    }
}
