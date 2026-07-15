using UnityEngine;
using UnityEngine.InputSystem;

namespace TheForest.Player
{
    /// <summary>
    /// Di chuyển FPS: đi, sprint, nhảy, trọng lực. Gắn lên capsule Player
    /// (có CharacterController). Nối với SurvivalStats và PlayerBlock: 
    /// Chặn sprint và giảm tốc độ di chuyển tương ứng theo từng vũ khí khi block.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Tốc độ di chuyển")]
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float sprintSpeed = 7f;
        [Tooltip("Tốc độ khi ngồi/rón (crouch) — lõi tàng hình SotF: đi chậm + thấp người để địch khó thấy.")]
        [SerializeField] private float crouchSpeed = 1.8f;

        [Header("Crouch (rón — tàng hình SotF)")]
        [SerializeField] private float standHeight = 2f;
        [SerializeField] private float crouchHeight = 1.1f;

        [Header("Nhảy & trọng lực")]
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -19.62f;

        [Header("Sinh tồn & Phòng thủ")]
        [SerializeField] private SurvivalStats stats;
        [SerializeField] private PlayerBlock playerBlock;
        [Tooltip("Stamina tối thiểu để bắt đầu sprint (tránh nhấp nháy khi gần cạn)")]
        [SerializeField] private float minStaminaToSprint = 5f;

        [Header("Tiếng bước chân (tàng hình SotF: rón gần như im, sprint rất ồn)")]
        [Tooltip("Để trống sẽ tự lấy PlayerMudCamo — áp NoiseMultiplier theo tư thế (rón/đứng yên).")]
        [SerializeField] private PlayerMudCamo stealth;
        [SerializeField] private float sprintStepLoudness = 1.4f;
        [SerializeField] private float walkStepLoudness = 0.7f;
        [SerializeField] private float crouchStepLoudness = 0.12f;
        [Tooltip("Quãng đường di chuyển ngang (m) giữa 2 lần phát tiếng bước.")]
        [SerializeField] private float stepDistance = 2.2f;

        private CharacterController _controller;
        private Vector2 _moveInput;
        private bool _sprintHeld;
        private bool _crouchHeld;
        private bool _jumpQueued;
        private float _verticalVelocity;
        private float _standCenterY;
        private float _stepAccum;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (stats == null) stats = GetComponent<SurvivalStats>();
            if (playerBlock == null) playerBlock = GetComponent<PlayerBlock>();
            if (stealth == null) stealth = GetComponent<PlayerMudCamo>();
            _standCenterY = _controller.center.y;
        }

        public void OnMove(InputValue value) => _moveInput = value.Get<Vector2>();
        public void OnSprint(InputValue value) => _sprintHeld = value.isPressed;
        public void OnCrouch(InputValue value) => _crouchHeld = value.isPressed;
        public void OnJump(InputValue value) { if (value.isPressed) _jumpQueued = true; }

        private void Update()
        {
            Vector3 move = transform.right * _moveInput.x + transform.forward * _moveInput.y;
            bool tryingToMove = _moveInput.sqrMagnitude > 0.01f;

            // Trích xuất trạng thái phòng thủ thời gian thực
            bool isBlocking = playerBlock != null && playerBlock.IsBlocking;

            // Crouch: ưu tiên hơn sprint (không thể vừa rón vừa chạy nước rút)
            bool isCrouching = _crouchHeld && _controller.isGrounded;

            // Điều kiện sprint: Đang giữ phím + di chuyển + còn stamina/energy + KHÔNG ĐANG BLOCK + KHÔNG rón
            bool canSprint = _sprintHeld && tryingToMove && !isBlocking && !isCrouching
                             && stats != null && stats.CanExert
                             && stats.StaminaCurrent > minStaminaToSprint;

            // Xác định vận tốc cơ sở dựa trên trạng thái chạy/rón
            float baseSpeed = isCrouching ? crouchSpeed : (canSprint ? sprintSpeed : walkSpeed);

            // Áp dụng hệ số giảm tốc độ đặc trưng trích xuất từ dữ liệu vũ khí đang cầm
            float speed = baseSpeed * (playerBlock != null ? playerBlock.MoveSpeedMultiplier : 1f);

            // Hạ/nâng chiều cao capsule theo crouch (thấp người để địch khó thấy)
            ApplyCrouchHeight(isCrouching);

            // Đẩy trạng thái sang SurvivalStats để nó xử lý tiêu/hồi stamina (wantSprint đã loại block nên không trừ nhầm)
            if (stats != null)
            {
                stats.IsSprinting = canSprint;
                stats.IsCrouching = isCrouching;
                stats.IsStill = !tryingToMove && _controller.isGrounded;
            }

            // Trọng lực + nhảy
            if (_controller.isGrounded)
            {
                if (_verticalVelocity < 0f) _verticalVelocity = -2f;
                if (_jumpQueued) _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            _jumpQueued = false;
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = move * speed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);

            // Tiếng bước chân: tích quãng đường NGANG, mỗi stepDistance phát 1 tiếng động cho Aic nghe.
            EmitFootstepNoise(tryingToMove, isCrouching, canSprint, (move * speed).magnitude * Time.deltaTime);
        }

        private void EmitFootstepNoise(bool moving, bool crouching, bool sprinting, float planarStep)
        {
            if (!moving || !_controller.isGrounded) { _stepAccum = 0f; return; }

            _stepAccum += planarStep;
            if (_stepAccum < stepDistance) return;
            _stepAccum = 0f;

            float loudness = crouching ? crouchStepLoudness : (sprinting ? sprintStepLoudness : walkStepLoudness);

            // Qua stealth để áp NoiseMultiplier theo tư thế (rón/đứng yên càng ẩn càng êm). Không có
            // component stealth thì phát thẳng — vẫn faithful vì loudness đã phân theo tư thế.
            if (stealth != null) stealth.EmitPlayerNoise(loudness);
            else TheForest.AI.NoiseSystem.EmitNoise(transform.position, loudness);
        }

        // Điều chỉnh chiều cao + tâm capsule khi rón, giữ chân chạm đất (đáy capsule cố định).
        private void ApplyCrouchHeight(bool crouching)
        {
            float targetH = crouching ? crouchHeight : standHeight;
            if (Mathf.Approximately(_controller.height, targetH)) return;

            float h = Mathf.MoveTowards(_controller.height, targetH, 8f * Time.deltaTime);
            _controller.height = h;
            // Giữ đáy capsule tại chỗ: center.y = standCenterY - (standHeight - h)/2
            var c = _controller.center;
            c.y = _standCenterY - (standHeight - h) * 0.5f;
            _controller.center = c;
        }
    }
}