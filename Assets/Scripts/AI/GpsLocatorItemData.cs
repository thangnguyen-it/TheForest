using UnityEngine;

namespace TheForest.Items
{
    /// <summary>
    /// Thiết bị định vị GPS (GDD thật: nhặt được trên xác lính, thường có chấm màu riêng trên la bàn/bản đồ).
    /// Tặng cho Virginia (qua VirginiaAI.TryReceiveItem) để theo dõi vị trí cô trên HUD.
    /// Bản thân item không mang logic — chỉ là "thẻ đánh dấu loại" để VirginiaAI nhận diện khi player
    /// đưa item này ra thay vì một vũ khí; UI la bàn/bản đồ (nếu có) tự hỏi VirginiaRegistry.TrackedPosition
    /// khi cần vẽ chấm định vị (trả null nếu chưa tặng GPS).
    /// </summary>
    [CreateAssetMenu(fileName = "GpsLocator_", menuName = "The Forest/Items/GPS Locator")]
    public class GpsLocatorItemData : ItemData
    {
    }
}
