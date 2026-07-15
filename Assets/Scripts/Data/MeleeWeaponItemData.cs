using UnityEngine;

namespace TheForest.Items
{
    /// <summary>
    /// Dữ liệu dùng CHUNG cho mọi vũ khí cận chiến KHÔNG PHẢI rìu (Machete, Katana, Crafted Club,
    /// Stun Baton, Utility Knife, Putter, Guitar, Electric Chainsaw...) — GDD thật xác nhận ~9 vũ khí
    /// cận chiến ngoài 3 loại Axe, tất cả chỉ khác nhau về DAMAGE/TỐC ĐỘ/BLOCK, không có logic riêng
    /// như rìu (rìu còn chặt được cây — xem AxeItemData). Vì vậy class này KHÔNG abstract và
    /// CreateAssetMenu được TRỰC TIẾP: Machete/Katana/Club là NHIỀU ASSET khác nhau của đúng 1 class
    /// này — xem WeaponArmorAssetSeeder.cs (Editor) để sinh hàng loạt bằng 1 cú click.
    /// </summary>
    [CreateAssetMenu(fileName = "Melee_", menuName = "The Forest/Items/Melee Weapon")]
    public class MeleeWeaponItemData : ItemData
    {
        [Header("Chỉ số cận chiến")]
        public float damage = 20f;

        [Tooltip("Số nhát vung/giây. Katana/Machete nhanh, Firefighter Axe chậm (GDD: chậm nhất nhưng damage cao nhất).")]
        public float swingSpeed = 1.2f;

        [Tooltip("Thời điểm (0..1 tiến trình animation) vũ khí thực sự chạm mục tiêu để áp sát thương.")]
        [Range(0f, 1f)] public float hitTimeNormalized = 0.45f;

        [Header("Đặc thù (tuỳ chọn)")]
        [Tooltip("Electric Chainsaw thật KHÔNG tốn stamina khi vung (đã kiểm chứng) — WeaponSwinger đọc cờ này để bỏ qua staminaPerSwing.")]
        public bool freeStamina = false;

        [Tooltip("Crafted Spear/Club/Guitar... có thể GÃY sau vài đòn TRÚNG (GDD thật). 0 = không giới hạn (Axe, Katana...).")]
        public int maxSwingsBeforeBreak = 0;

        [Header("Stun khi trúng (Stun Baton — GDD xác nhận VỪA gây damage VỪA Stun, khác Stun Gun chỉ Stun)")]
        public bool appliesStunOnHit = false;
        public float meleeStunDuration = 2.5f;
    }
}
