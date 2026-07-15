using UnityEngine;
using TheForest.Building.Core;
using TheForest.Building.Events;

namespace TheForest.World
{
    /// <summary>
    /// FIX (đối chiếu Phần A.2 — báo cáo roadmap): hai kiến trúc event khác nhau đang cùng tồn tại
    /// trong dự án — hệ Building mới dùng <c>EventBus&lt;T&gt;</c> tĩnh, hệ AI/World cũ dùng
    /// ScriptableObject Channel + <see cref="FireRegistry"/>. BuildingController.OnFirePlaced() ghi chú
    /// "FireSource system subscribes independently — no direct call needed", nhưng KHÔNG có script nào
    /// từng thực sự subscribe <see cref="StructureFirePlacedEvent"/> — nghĩa là khi Kelvin xây lửa
    /// (KelvinBuildingCommands.BuildFireRoutine) raise sự kiện này, chưa từng có đống lửa nào được sinh.
    ///
    /// Class này là cầu nối THẬT giữa 2 hệ: nghe EventBus&lt;StructureFirePlacedEvent&gt;, Instantiate
    /// prefab lửa, rồi gọi FireSource.SetBurning(true) (API công khai có sẵn) để FireRegistry nhận diện
    /// ngay lập tức — cannibal sẽ sợ & né ra giống hệt lửa do người chơi tự dựng bằng CampfireController.
    ///
    /// Đặt 1 instance trong scene (khuyến nghị: cùng GameObject với BuildingController).
    /// </summary>
    public class StructureFireSpawner : MonoBehaviour
    {
        [Tooltip("Prefab lửa: chỉ cần có component FireSource (+ hiệu ứng cháy tuỳ chọn). " +
                 "Có thể dùng chung prefab đã có CampfireController nếu muốn Kelvin dựng ra một đống lửa " +
                 "TƯƠNG TÁC được như bình thường — SetBurning(true) ở đây coi như 'đã mồi sẵn' cho Kelvin, " +
                 "bỏ qua bước đặt 2 que + giữ E mồi lửa thủ công mà CampfireController yêu cầu với player.")]
        [SerializeField] private GameObject firePrefab;

        private void OnEnable() => EventBus<StructureFirePlacedEvent>.Subscribe(OnFirePlaced);
        private void OnDisable() => EventBus<StructureFirePlacedEvent>.Unsubscribe(OnFirePlaced);

        private void OnFirePlaced(StructureFirePlacedEvent e)
        {
            if (firePrefab == null)
            {
                Debug.LogWarning("[StructureFireSpawner] Chưa gán firePrefab trong Inspector — không thể dựng lửa hộ Kelvin.");
                return;
            }

            var go = Instantiate(firePrefab, e.Position, Quaternion.identity);

            var fireSource = go.GetComponent<FireSource>();
            if (fireSource != null)
                fireSource.SetBurning(true);
            else
                Debug.LogWarning("[StructureFireSpawner] firePrefab thiếu component FireSource — cannibal sẽ KHÔNG sợ đống lửa này.");
        }
    }
}
