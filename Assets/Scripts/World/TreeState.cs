using System;
using UnityEngine;

namespace TheForest.World
{
    /// <summary>
    /// Trạng thái một vị trí cây (slot) theo mô hình GDD:
    /// - Standing: cây đứng, chặt được.
    /// - Stump   : đã đổ, còn gốc; có thể mọc lại (10% mỗi khi ngủ).
    /// - Removed : đã đào gốc -> loại khỏi vòng regrow vĩnh viễn.
    /// </summary>
    public enum TreeState
    {
        Standing,
        Stump,
        Removed
    }

}