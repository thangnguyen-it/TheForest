using UnityEngine;

namespace TheForest.Items
{
    /// <summary>
    /// Súng — Pistol/Revolver/Shotgun/Rifle đều dùng CHUNG class này (chỉ khác số liệu asset), giống
    /// hệt cách MeleeWeaponItemData dùng chung cho Machete/Katana/Club. Đã kiểm chứng qua tra cứu:
    /// Shotgun = damage cao nhất nhưng chậm/ít đạn; Revolver = damage cao hơn Pistol nhưng vẫn bắn đạn
    /// 9mm y như Pistol; Rifle (Patch 07) = tầm bắn thực tế KHÔNG xa như hình dung dù có ống ngắm.
    ///
    /// FIX fidelity (#6): súng thường nay HITSCAN (raycast tức thời) — KHÔNG còn đầu đạn Rigidbody bay
    /// theo thời gian. Chỉ Bow/Crossbow/Slingshot mới là projectile trong game gốc.
    ///
    /// FIX fidelity (#8): SỐ VIÊN toả (Buckshot vs Slug) chuyển sang thuộc tính của ĐẠN (AmmoItemData),
    /// KHÔNG còn cứng trên súng — nhờ đó cùng khẩu Shotgun nạp Buckshot hay Slug đều đúng.
    ///
    /// Stun Gun: isStunWeapon=true, caliber=StunCartridge. KHÔNG hitscan — vẫn bắn 1 projectile taser
    /// (Bullet.cs, isStun=true) để Stun mục tiêu; nạp CARTRIDGE mang theo như đạn thường (Stun Gun thật
    /// không phải "pin vô hạn"). Puffy/Blind Mutant đặc biệt yếu trước nó; mutant Pale/Creepy kháng Stun.
    /// </summary>
    [CreateAssetMenu(fileName = "Firearm_", menuName = "The Forest/Items/Firearm")]
    public class FirearmItemData : ItemData
    {
        [Header("Đạn tương thích")]
        public AmmoCaliber caliber = AmmoCaliber.Pistol9mm;

        [Header("Băng đạn")]
        public int magazineSize = 8;
        public float reloadSeconds = 1.8f;

        [Header("Bắn")]
        public float fireCooldown = 0.25f;
        [Tooltip("Tầm HITSCAN tối đa (m) cho súng thường. Bỏ qua nếu isStunWeapon (dùng projectile).")]
        public float hitscanRange = 120f;

        [Header("Non-lethal (Stun Gun — projectile taser)")]
        public bool isStunWeapon = false;
        public float stunDuration = 3f;
        [Tooltip("Tốc độ đầu taser bay ra (chỉ dùng khi isStunWeapon). Súng thường KHÔNG dùng.")]
        public float stunProjectileSpeed = 55f;
    }
}
