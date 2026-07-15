namespace TheForest.AI
{
    /// <summary>Bộ lạc cannibal theo GDD, mở khóa dần theo ngày.</summary>
    public enum CannibalTribe
    {
        Starving,   // loại riêng, hung hãn từ đầu
        Regular,
        PaleSkinny, // ~ngày 5
        Pale,       // ~ngày 6
        Painted,    // giữa-cuối
        Masked      // ~ngày 22
    }
}
