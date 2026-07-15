using UnityEngine;

namespace TheForest.Items
{
    // FIX fidelity (#7): Revolver KHÔNG có caliber riêng — nó bắn CÙNG đạn 9mm với Pistol (đã kiểm chứng),
    // nên gộp về Pistol9mm. Rifle giữ (thêm ở Patch 07). StunCartridge = hộp taser cho Stun Gun
    // (Stun Gun thật nạp CARTRIDGE mang theo mỗi phát, không phải "pin vô hạn" như bản cũ).
    public enum AmmoCaliber { Pistol9mm, Shotgun, Rifle, StunCartridge }

    /// <summary>
    /// Đạn súng. FirearmItemData.caliber phải khớp AmmoItemData.caliber thì mới nạp được.
    ///
    /// FIX fidelity (#8 — Shotgun Slug vs Buckshot): SỐ VIÊN toả ra là thuộc tính của ĐẠN, không phải súng.
    /// Cùng khẩu Shotgun nạp:
    ///   - Buckshot shell: pellets=8, spreadAngle≈8 (toả rộng, tầm gần) — pellets>1.
    ///   - Slug shell: pellets=1, spreadAngle=0, damage cao (một viên xuyên, tầm xa hơn).
    /// Pistol/Rifle: pellets=1, spread=0.
    ///
    /// Súng thường nay HITSCAN (raycast) — KHÔNG còn prefab đầu đạn bay. bulletProjectilePrefab cũ ĐÃ BỎ;
    /// chỉ giữ impactVfxPrefab tuỳ chọn để hiện vệt trúng.
    /// </summary>
    [CreateAssetMenu(fileName = "Ammo_", menuName = "The Forest/Items/Ammo")]
    public class AmmoItemData : ItemData
    {
        public AmmoCaliber caliber = AmmoCaliber.Pistol9mm;
        public float damage = 15f;

        [Header("Toả đạn (Buckshot vs Slug)")]
        [Tooltip("Số viên bay ra mỗi phát. Buckshot=8, Slug/Pistol/Rifle=1.")]
        public int pellets = 1;
        [Tooltip("Góc toả (độ) khi pellets>1. Slug/Pistol/Rifle = 0.")]
        public float spreadAngle = 0f;

        [Header("Hiệu ứng (tuỳ chọn)")]
        [Tooltip("VFX tại điểm trúng của phát hitscan (tia lửa/lỗ đạn). Để trống nếu chưa có.")]
        public GameObject impactVfxPrefab;

        [Header("Projectile (CHỈ dùng cho đạn StunCartridge — súng thường hitscan không cần)")]
        [Tooltip("Đầu taser bay ra (model + component Bullet.cs). Chỉ cần cho đạn Stun Gun.")]
        public GameObject stunProjectilePrefab;
    }
}
