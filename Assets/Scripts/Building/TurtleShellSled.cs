using UnityEngine;
using UnityEngine.InputSystem;
using TheForest.Interaction;

namespace TheForest.Building
{
    /// <summary>
    /// Mai rùa làm sled: đặt xuống dốc, player ngồi lên trượt theo trọng lực.
    /// Nhìn vào + E: lên/xuống sled. Khi cưỡi, parent player vào sled, sled dùng physics trượt.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TurtleShellSled : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform seat;          // chỗ ngồi
        [SerializeField] private float steerTorque = 5f;  // lái trái/phải
        [SerializeField] private float maxSpeed = 18f;

        private Rigidbody _rb;
        private Transform _rider;
        private MonoBehaviour[] _riderControlsToDisable; // PlayerController... khi cưỡi
        private bool _mounted;

        private void Awake() { _rb = GetComponent<Rigidbody>(); }

        public string GetPrompt() => _mounted ? "[E] Xuống" : "[E] Cưỡi mai rùa";
        public bool CanInteract() => true;

        public void Interact(GameObject interactor)
        {
            if (_mounted) Dismount();
            else Mount(interactor);
        }
        public void OnFocus() { }
        public void OnLoseFocus() { }

        private void Mount(GameObject player)
        {
            _rider = player.transform;
            _mounted = true;

            // tắt điều khiển đi bộ trong lúc trượt (tùy hệ của bạn)
            var pc = player.GetComponent<TheForest.Player.PlayerController>();
            if (pc != null) pc.enabled = false;

            // gắn player lên ghế
            _rider.SetParent(seat != null ? seat : transform);
            if (seat != null) { _rider.localPosition = Vector3.zero; }
            // vô hiệu CharacterController nếu có (tránh xung đột physics)
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
        }

        private void Dismount()
        {
            if (_rider != null)
            {
                var cc = _rider.GetComponent<CharacterController>();
                _rider.SetParent(null);
                _rider.position = transform.position + Vector3.up * 1f + transform.right * 1.5f;
                if (cc != null) cc.enabled = true;
                var pc = _rider.GetComponent<TheForest.Player.PlayerController>();
                if (pc != null) pc.enabled = true;
            }
            _rider = null; _mounted = false;
        }

        // Lái khi đang cưỡi (input A/D qua action Move, hoặc đọc trực tiếp)
        private void FixedUpdate()
        {
            if (!_mounted) return;
            // giới hạn tốc độ
            if (_rb.linearVelocity.magnitude > maxSpeed)
                _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;
        }

        // gọi từ input lái (tùy chọn)
        public void Steer(float horizontal)
        {
            if (!_mounted) return;
            _rb.AddTorque(Vector3.up * horizontal * steerTorque, ForceMode.Acceleration);
        }
    }
}
