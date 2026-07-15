namespace TheForest.Interaction
{
    /// <summary>
    /// Mở rộng CÓ CHỌN LỌC (opt-in) cho IInteractable: đối tượng muốn nhận Interact() LIÊN TỤC
    /// mỗi frame trong lúc người chơi GIỮ phím tương tác, thay vì chỉ 1 lần khi vừa nhấn.
    /// Ví dụ: giữ E để mồi lửa campfire (CampfireController), giữ E để sửa công trình...
    ///
    /// QUAN TRỌNG: đây là interface RIÊNG, không sửa IInteractable gốc -> KHÔNG phá vỡ bất kỳ
    /// implementer nào đang có (ItemPickup, RabbitCage, AnimalTrap, TreeCutting...). Những class đó
    /// không cài IHoldInteractable nên hành vi "nhấn 1 lần" của chúng giữ nguyên 100%.
    /// InteractionRaycaster chỉ gọi Interact() liên tục khi _current is IHoldInteractable VÀ
    /// HoldToInteract == true tại thời điểm đó (có thể đổi true/false theo state nội bộ của đối tượng).
    /// </summary>
    public interface IHoldInteractable : IInteractable
    {
        /// <summary>
        /// true = đang ở trạng thái muốn nhận Interact() liên tục khi giữ phím.
        /// Có thể trả về false ở những state khác của CHÍNH đối tượng đó để quay lại hành vi nhấn-1-lần
        /// (vd CampfireController: chỉ true khi đã đủ que và đang chờ mồi lửa; false khi đang đặt từng que).
        /// </summary>
        bool HoldToInteract { get; }
    }
}
