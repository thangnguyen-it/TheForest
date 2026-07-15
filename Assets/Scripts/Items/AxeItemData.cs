using UnityEngine;

namespace TheForest.Items
{
    /// <summary>
    /// Rìu — vũ khí cận chiến DUY NHẤT chặt được cây (EquipmentController.GetChopDamage() nhận diện
    /// qua type check `is AxeItemData`, KHÔNG áp dụng cho Machete/Katana/Club...).
    ///
    /// FIX (Giai đoạn 3): trước đây khai báo RIÊNG 3 field chopDamage/swingSpeed/hitTimeNormalized,
    /// khiến WeaponSwinger chỉ nhận diện được mỗi Axe (`as AxeItemData`) — mọi vũ khí cận chiến khác
    /// (Machete, Katana...) sẽ vung ra 0 damage. Giờ kế thừa MeleeWeaponItemData để dùng CHUNG
    /// damage/swingSpeed/hitTimeNormalized với mọi vũ khí cận chiến khác.
    ///
    /// ⚠️ MIGRATION: nếu bạn đã có asset Axe cũ với giá trị chopDamage đã điền sẵn trong Inspector,
    /// giá trị đó sẽ KHÔNG tự chuyển sang field `damage` mới (Unity serialize theo TÊN field) — cần mở
    /// lại từng asset Axe hiện có và điền lại 3 số damage/swingSpeed/hitTimeNormalized theo field mới.
    /// </summary>
    [CreateAssetMenu(fileName = "New Axe Item Data", menuName = "The Forest/Items/Axe")]
    public class AxeItemData : MeleeWeaponItemData
    {
    }
}
