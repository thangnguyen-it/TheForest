using System;
using System.Collections.Generic;
using UnityEngine;
using TheForest.Items;

namespace TheForest.Player
{
    /// <summary>
    /// Quản lý các mảnh giáp đang mặc — MODEL CHARGE (khớp Sons of the Forest thật), thay cho model
    /// "giảm % vĩnh viễn" kiểu The Forest mà bản cũ dùng nhầm.
    ///
    /// SotF thật: mặc tối đa maxPieces mảnh trên người (không phân slot Đầu/Ngực/Chân). Khi trúng đòn,
    /// TỪNG mảnh lần lượt HẤP THỤ bớt sát thương (absorbPerHit) và TỤT 1 nấc độ bền; hết nấc thì mảnh GÃY
    /// và biến mất. Đánh nhiều thì giáp vỡ dần — KHÔNG phải giảm % cố định.
    ///
    /// NGOẠI LỆ: mảnh unbreakable (Golden/Ancient) không tiêu độ bền — chỉ giảm % cố định, cộng dồn tách
    /// riêng ở FlatReduction.
    ///
    /// PlayerDamageReceiver gọi ProcessIncoming(rawDamage) SAU block để lấy sát thương còn lại.
    ///
    /// LƯU Ý (fidelity): bản cũ tự đẩy stealth sang PlayerMudCamo.SetArmorStealth() — cơ chế "giáp lá =
    /// tàng hình" đó là của The Forest, KHÔNG có trong SotF, nên ĐÃ GỠ khỏi đây. Hệ mud/warpaint gốc giữ
    /// nguyên, chỉ không còn được giáp nuôi stealth nữa.
    /// </summary>
    public class PlayerArmor : MonoBehaviour
    {
        [Tooltip("Tổng số mảnh giáp tiêu hao đội được cùng lúc (SotF: ~10 slot trên người).")]
        [SerializeField] private int maxPieces = 10;

        // Một mảnh giáp đang mặc + số đòn còn lại trước khi gãy (chỉ dùng cho mảnh KHÔNG unbreakable).
        private class WornPiece
        {
            public ArmorItemData armor;
            public int hitsRemaining;
        }

        // Xếp hàng theo thứ tự mặc (mảnh mặc trước chịu đòn trước). Danh sách phẳng = mỗi phần tử 1 mảnh.
        private readonly List<WornPiece> _breakable = new List<WornPiece>();
        private readonly List<ArmorItemData> _unbreakable = new List<ArmorItemData>();

        /// <summary>Tổng % giảm cố định từ các mảnh unbreakable (Golden/Ancient), kẹp 0..0.9.</summary>
        public float FlatReduction { get; private set; }

        /// <summary>Tổng số mảnh đang mặc (tiêu hao + bất hoại).</summary>
        public int TotalPieces => _breakable.Count + _unbreakable.Count;

        public event Action OnArmorChanged;
        /// <summary>Bắn ra khi một mảnh giáp gãy (để UI/SFX phản hồi).</summary>
        public event Action<ArmorItemData> OnPieceBroke;

        public int GetWornCount(ArmorItemData armor)
        {
            if (armor == null) return 0;
            int c = 0;
            if (armor.unbreakable)
            {
                foreach (var a in _unbreakable) if (a == armor) c++;
            }
            else
            {
                foreach (var p in _breakable) if (p.armor == armor) c++;
            }
            return c;
        }

        /// <summary>Mặc thêm 1 mảnh (tiêu từ Inventory). Trả false nếu đã đủ maxPieces hoặc không có trong túi.</summary>
        public bool TryEquipPiece(ArmorItemData armor, Inventory inventory)
        {
            if (armor == null || TotalPieces >= maxPieces) return false;
            if (inventory != null && !inventory.TryConsume(armor, 1)) return false;

            if (armor.unbreakable)
                _unbreakable.Add(armor);
            else
                _breakable.Add(new WornPiece { armor = armor, hitsRemaining = Mathf.Max(1, armor.hitsUntilBreak) });

            Recompute();
            OnArmorChanged?.Invoke();
            return true;
        }

        /// <summary>Cởi 1 mảnh còn nguyên, trả về lại Inventory. Mảnh đã gãy thì không cởi được (đã mất).</summary>
        public bool TryUnequipPiece(ArmorItemData armor, Inventory inventory)
        {
            if (armor == null) return false;

            if (armor.unbreakable)
            {
                int idx = _unbreakable.LastIndexOf(armor);
                if (idx < 0) return false;
                _unbreakable.RemoveAt(idx);
            }
            else
            {
                int idx = _breakable.FindLastIndex(p => p.armor == armor);
                if (idx < 0) return false;
                _breakable.RemoveAt(idx);
            }

            inventory?.Add(armor, 1);
            Recompute();
            OnArmorChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Cho sát thương đi qua giáp. Trả về sát thương CÒN LẠI truyền tới máu.
        /// Trình tự (khớp SotF): mảnh unbreakable giảm % trước; rồi ĐÚNG MỘT mảnh tiêu hao hấp thụ đòn này
        /// (soak tối đa absorbPerHit — đủ khoẻ thì chặn trọn đòn) và TỤT 1 nấc độ bền. "Mỗi đòn ăn 1 nấc
        /// của 1 mảnh" — một cú đánh KHÔNG phá cả chồng giáp cùng lúc. Mảnh cạn nấc thì gãy & biến mất.
        /// </summary>
        public float ProcessIncoming(float rawDamage)
        {
            if (rawDamage <= 0f) return rawDamage;

            float dmg = rawDamage * (1f - FlatReduction);
            if (_breakable.Count == 0) return dmg;

            var piece = _breakable[0]; // mảnh mặc sớm nhất chịu đòn trước
            dmg -= Mathf.Min(dmg, piece.armor.absorbPerHit);
            piece.hitsRemaining--;

            if (piece.hitsRemaining <= 0)
            {
                _breakable.RemoveAt(0);
                OnPieceBroke?.Invoke(piece.armor);
            }

            OnArmorChanged?.Invoke();
            return dmg;
        }

        private void Recompute()
        {
            float flat = 0f;
            foreach (var a in _unbreakable) flat += a.flatReductionPercent;
            FlatReduction = Mathf.Clamp(flat, 0f, 0.9f);
        }
    }
}
