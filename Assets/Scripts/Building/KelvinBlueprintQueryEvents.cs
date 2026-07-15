using UnityEngine;
using TheForest.Building.Data;

namespace TheForest.Building.Events
{
    /// <summary>
    /// Cầu nối Kelvin (KelvinBuildingCommands) &lt;-&gt; BlueprintSystem QUA EventBus — fix bug Block 7
    /// ("KelvinBuildingCommands.cs: Dùng FindFirstObjectByType&lt;BlueprintSystem&gt;() tại runtime — vi phạm
    /// kiến trúc no Manager-to-Manager"). Kelvin RAISE một query, BlueprintSystem (đã tồn tại sẵn, chỉ
    /// SUBSCRIBE thêm 2 handler nhỏ) lắng nghe và RAISE kết quả/thực thi hộ.
    ///
    /// EventBus&lt;T&gt;.Raise chạy ĐỒNG BỘ (xem EventBus.cs: vòng for gọi handler trực tiếp, không deferred)
    /// nên phía Kelvin có thể Raise query rồi đọc kết quả NGAY sau đó trong cùng dòng lệnh, không cần
    /// chờ thêm khung hình — giữ nguyên ngữ nghĩa "gọi hàm đồng bộ" như code cũ, chỉ khác là đi qua bus
    /// thay vì tham chiếu trực tiếp.
    /// </summary>
    public readonly struct KelvinBlueprintQueryEvent
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public KelvinBlueprintQueryEvent(Vector3 pos, float radius) { Position = pos; Radius = radius; }
    }

    public readonly struct KelvinBlueprintQueryResultEvent
    {
        public readonly GameObject Ghost;
        public readonly BlueprintData Data;
        public KelvinBlueprintQueryResultEvent(GameObject ghost, BlueprintData data) { Ghost = ghost; Data = data; }
    }

    public readonly struct KelvinBlueprintAddMaterialEvent
    {
        public readonly string MaterialId;
        public readonly int Amount;
        public KelvinBlueprintAddMaterialEvent(string materialId, int amount) { MaterialId = materialId; Amount = amount; }
    }
}
