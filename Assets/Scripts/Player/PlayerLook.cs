using UnityEngine;
using UnityEngine.InputSystem;

namespace TheForest.Player
{
    /// <summary>
    /// Camera FPS: chuột ngang xoay thân player, chuột dọc ngẩng/cúi camera.
    /// Gắn lên cùng object Player; gán cameraTransform là Camera con.
    /// </summary>
    public class PlayerLook : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private Transform cameraTransform;

        [Header("Độ nhạy chuột")]
        [SerializeField] private float sensitivity = 0.1f;
        [SerializeField] private float maxPitch = 89f;

        private Vector2 _lookInput;
        private float _pitch;

        public void OnLook(InputValue value) => _lookInput = value.Get<Vector2>();

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked; // khóa chuột vào giữa màn hình
            Cursor.visible = false;
        }

        public bool LookEnabled { get; set; } = true;

        private void Update()
        {
            if (!LookEnabled) return; // Dừng xoay camera khi đang mở UI túi đồ

            // Ngang: xoay cả thân player quanh trục Y
            transform.Rotate(Vector3.up * _lookInput.x * sensitivity);

            // Dọc: chỉ xoay camera, kẹp góc để không lộn đầu
            _pitch -= _lookInput.y * sensitivity;
            _pitch = Mathf.Clamp(_pitch, -maxPitch, maxPitch);
            cameraTransform.localEulerAngles = new Vector3(_pitch, 0f, 0f);
        }

    }
}