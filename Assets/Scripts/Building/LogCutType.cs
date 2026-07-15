namespace TheForest.Building
{
    /// <summary>
    /// Enum hợp nhất cho mọi kiểu cắt gỗ — dùng chung giữa Tree System và Building System.
    /// - Cắt ngang (hamburger style): Full -> ThreeQuarter / Half / Quarter.
    /// - Cắt dọc (hotdog style): -> Plank và các biến thể. Plank KHÔNG đặt đứng được.
    /// - Firewood: cắt tiếp từ Half theo chiều ngang, dùng làm nhiên liệu cho lửa.
    /// </summary>
    public enum LogCutType
    {
        Full,
        ThreeQuarter,
        Half,
        Quarter,
        Plank,
        PlankThreeQuarter,
        PlankHalf,
        PlankQuarter,
        Firewood
    }
}
