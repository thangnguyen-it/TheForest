namespace TheForest.Building
{
    // ─── LOG / MATERIAL ───────────────────────────────────────────────────────
    public enum LogType
    {
        Full,           // 1.0  — chặt cây
        ThreeQuarter,   // 0.75 — cắt ngang tại vạch 3/4
        Half,           // 0.5  — cắt ngang tại vạch 1/2
        Quarter,        // 0.25 — cắt ngang tại vạch 1/4
        Split,          // plank — cắt dọc full log
        SplitQuarter,   // half-plank — cắt tiếp split log
        Stick,          // nhặt hoặc chặt bụi
        Stone           // đá lớn
    }

    // ─── PLACEMENT ────────────────────────────────────────────────────────────
    public enum PlacementMode
    {
        Horizontal, // log nằm ngang (default)
        Vertical,   // log dọc xuống đất (palisade, cột)
        Diagonal    // log tựa nghiêng 45°
    }

    // ─── INDICATOR ────────────────────────────────────────────────────────────
    public enum IndicatorType
    {
        None,
        HorizontalLog,    // white rectangle — đặt log nằm ngang
        VerticalLog,      // white circle + dot — đặt log dọc
        SnapArrow,        // white arrow — snap vào log khác
        ConnectionDash,   // white dashed line — nối hai log dọc thành tường
        DiagonalLog,      // white diagonal arrow — log tựa chéo 45°
        CutMarkWidth,     // red dashed horizontal — cắt ngang log
        CutMarkSplit,     // red vertical line — cắt dọc tạo plank
        InvalidPlacement, // red silhouette — không thể đặt
        DismantleHighlight,
        SpikeIndicator,   // red spike — đầu log sẽ được vát nhọn
        StrutIndicator,   // white L-shape at corner
        StairsIndicator,  // white wavy arrow — split log → bậc thang
        DoorCutMark       // red column mark — vị trí cắt cửa
    }

    // ─── CUTTING ─────────────────────────────────────────────────────────────
    public enum CutActionType { Width, Length, Sharpen }

    // ─── DAMAGE ───────────────────────────────────────────────────────────────
    public enum DamageState { Undamaged, Damaged, Destroyed }

    // ─── TARP ────────────────────────────────────────────────────────────────
    public enum TarpMode { BasicShelter, FourCorner, Trampoline }

    // ─── ELECTRICITY ─────────────────────────────────────────────────────────
    public enum PowerNodeType { Source, Wire, Consumer }

    // ─── KELVIN ──────────────────────────────────────────────────────────────
    public enum KelvinBuildingTask
    {
        FillLogHolder,
        FetchLogs,
        BuildFire,
        BuildShelter,
        FinishBlueprint,
        RepairStructure,
        ClearArea
    }

    // ─── STONE ───────────────────────────────────────────────────────────────
    public enum StoneBuildingType { Floor, Wall, Pillar, Door, Window }

    // ─── WALL ────────────────────────────────────────────────────────────────
    public enum WallType { Standard, Palisade, Stone }

    // ─── ROOF ────────────────────────────────────────────────────────────────
    public enum RoofType { Flat, Slanted, Tarp }
}