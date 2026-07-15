using UnityEngine;

namespace TheForest.Items
{
    // GDD thật (đã kiểm chứng): Golden Armor và "Ancient" là CÙNG một bộ giáp — không tách 2 tier.
    // Vì vậy enum bỏ 'Ancient', gộp về 'Golden'. Creepy = lột từ mutant. Bone/Tech = tier bảo vệ cao.
    public enum ArmorTier { Leaf, Hide, Bone, Tech, Creepy, Golden }

    /// <summary>
    /// Một MẢNH giáp — MODEL CHARGE (hấp thụ theo SỐ ĐÒN), khớp Sons of the Forest thật thay vì model
    /// "giảm % vĩnh viễn" kiểu The Forest (2014) mà bản cũ dùng nhầm.
    ///
    /// SotF thật: mặc NHIỀU MẢNH cùng lúc (tối đa 10 slot trên người). Mỗi mảnh HẤP THỤ trọn phần lớn
    /// một đòn rồi TỤT ĐỘ BỀN 1 nấc; hết nấc thì GÃY và biến mất (không còn bảo vệ). Đây KHÔNG phải giảm
    /// % cố định — giáp là "lớp đệm tiêu hao", đánh mãi sẽ vỡ.
    ///
    /// NGOẠI LỆ DUY NHẤT: Golden/Ancient Armor KHÔNG vỡ — nó là % giảm sát thương cố định
    /// (unbreakable=true + flatReductionPercent). Đây là tier duy nhất đúng với model "%".
    ///
    /// ⚠️ MIGRATION: bản cũ có field stealthBonusPerPiece + protectionBonusPerPiece (%) — ĐÃ BỎ.
    /// Cơ chế tàng hình-theo-giáp ("10 mảnh Leaf = 100% ẩn") là của The Forest, KHÔNG có trong SotF nên
    /// gỡ khỏi giáp. Asset Armor cũ (nếu có) cần điền lại absorbPerHit/hitsUntilBreak theo field mới.
    /// </summary>
    [CreateAssetMenu(fileName = "Armor_", menuName = "The Forest/Items/Armor")]
    public class ArmorItemData : ItemData
    {
        [Header("Loại giáp")]
        public ArmorTier tier = ArmorTier.Hide;

        [Header("Charge — mỗi MẢNH hấp thụ theo SỐ ĐÒN (bỏ qua nếu unbreakable)")]
        [Tooltip("Lượng sát thương một mảnh soak được cho MỖI đòn. Leaf thấp, Bone/Tech cao.")]
        public float absorbPerHit = 15f;
        [Tooltip("Số đòn mảnh chịu được trước khi GÃY. Leaf ~1, Hide ~2, Bone ~3, Tech ~4.")]
        public int hitsUntilBreak = 3;

        [Header("Ngoại lệ Golden/Ancient — KHÔNG vỡ, giảm % cố định")]
        [Tooltip("Chỉ Golden/Ancient = true. Khi true, absorbPerHit/hitsUntilBreak bị bỏ qua.")]
        public bool unbreakable = false;
        [Tooltip("Giảm % sát thương cố định khi unbreakable (Golden ~0.3 = 30%).")]
        [Range(0f, 1f)] public float flatReductionPercent = 0.3f;
    }
}
