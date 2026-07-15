using UnityEngine;

namespace TheForest.Interaction
{
    /// <summary>
    /// Mọi vật tương tác được (nhặt đồ, chặt cây, dùng giường, mở rương...) cài interface này.
    /// InteractionRaycaster sẽ phát hiện và gọi các hàm tương ứng.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Dòng prompt hiển thị khi nhìn vào (vd "Nhặt Rìu đá", "Ngủ").</summary>
        string GetPrompt();

        /// <summary>Có cho phép tương tác lúc này không (vd rương rỗng, cây đã đổ).</summary>
        bool CanInteract();

        /// <summary>Người chơi nhấn phím tương tác (E). Truyền vào người gọi để xử lý context.</summary>
        void Interact(GameObject interactor);

        /// <summary>Bắt đầu nhìn vào (highlight, viền sáng...). Tùy chọn.</summary>
        void OnFocus();

        /// <summary>Thôi nhìn (tắt highlight). Tùy chọn.</summary>
        void OnLoseFocus();
    }
}
