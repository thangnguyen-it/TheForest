using UnityEngine;
using UnityEngine.InputSystem;

namespace TheForest.Interaction
{
    /// <summary>
    /// Raycast FPS từ camera để phát hiện vật IInteractable trước mặt.
    /// Quản lý focus (highlight) và gọi Interact khi nhấn phím tương tác.
    /// Tích hợp ưu tiên: Ám sát (Stealth Kill) chạy trước mọi tương tác khác!
    /// </summary>
    public class InteractionRaycaster : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private Transform cameraTransform;

        [Header("Raycast")]
        [SerializeField] private float interactRange = 3f;
        [Tooltip("Layer các vật tương tác được. Để Everything nếu chưa phân layer.")]
        [SerializeField] private LayerMask interactMask = ~0;
        [Tooltip("Bỏ qua trigger collider khi raycast")]
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Auto-swing (giữ E để chặt liên tục)")]
        [SerializeField] private Player.WeaponSwinger swinger;

        [Header("Ám sát (Stealth Kill)")]
        [SerializeField] private TheForest.Player.StealthKillController stealthKill;

        // Vật đang được nhìn
        private IInteractable _current;
        private bool _interactQueued;
        private bool _interactHeld;
        private IChoppable _currentChoppable;
        private RaycastHit _lastHit;

        // Cho HUD đọc để hiện/ẩn prompt
        public string CurrentPrompt { get; private set; }

        // Sửa đổi: Báo HasTarget nếu trúng vật phẩm HOẶC đủ điều kiện móc lốp quái
        public bool HasTarget => (stealthKill != null && stealthKill.HasStealthTarget) || (_current != null && _current.CanInteract());

        public event System.Action<string> OnPromptChanged;

        private void Awake()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            if (swinger == null)
                swinger = GetComponent<Player.WeaponSwinger>();

            if (stealthKill == null)
                stealthKill = GetComponent<TheForest.Player.StealthKillController>();
        }

        public void OnInteract(InputValue value)
        {
            bool pressed = value.isPressed;
            _interactHeld = pressed;
            if (pressed) _interactQueued = true;
        }

        private void Update()
        {
            DetectTarget();

            // Nếu đang giữ E để chặt cây/đánh địch
            if (_interactHeld && swinger != null && _currentChoppable != null && _currentChoppable.CanBeChopped())
            {
                swinger.RequestSwing(_currentChoppable);
                _interactQueued = false;
                return;
            }

            // BỔ SUNG: giữ phím liên tục cho đối tượng tự khai báo qua IHoldInteractable (vd mồi lửa campfire).
            // CHỈ kích hoạt khi chính đối tượng đang yêu cầu (HoldToInteract == true lúc này) -> không ảnh hưởng
            // hành vi nhấn-1-lần mặc định của mọi IInteractable khác (ItemPickup, RabbitCage, AnimalTrap...).
            if (_interactHeld && _current is IHoldInteractable holdTarget && holdTarget.HoldToInteract && _current.CanInteract())
            {
                _current.Interact(gameObject);
                _interactQueued = false;
                return;
            }

            if (_interactQueued)
            {
                _interactQueued = false;

                // ƯU TIÊN 1: ÁM SÁT (STEALTH KILL)
                if (stealthKill != null && stealthKill.HasStealthTarget && stealthKill.TryExecute())
                {
                    return; // Nếu đâm thành công, hủy bỏ mọi tương tác nhặt rác/nhặt gỗ phía sau
                }

                // ƯU TIÊN 2: TƯƠNG TÁC BÌNH THƯỜNG (Nhặt, Mở hòm...)
                if (_current != null && _current.CanInteract())
                {
                    _current.Interact(gameObject);
                }
            }
        }

        private void DetectTarget()
        {
            IInteractable found = null;

            if (cameraTransform != null)
            {
                Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
                if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask, triggerInteraction))
                {
                    found = hit.collider.GetComponentInParent<IInteractable>();
                    _currentChoppable = hit.collider.GetComponentInParent<IChoppable>();
                    _lastHit = hit;
                }
                else
                {
                    _currentChoppable = null;
                }
            }

            if (found != _current)
            {
                _current?.OnLoseFocus();
                _current = found;
                _current?.OnFocus();

                UpdatePrompt();
            }
            else
            {
                UpdatePrompt();
            }
        }

        private void UpdatePrompt()
        {
            string newPrompt = string.Empty;

            // KIỂM TRA CHỮ HIỂN THỊ (ƯU TIÊN ÁM SÁT TRƯỚC)
            if (stealthKill != null && stealthKill.HasStealthTarget)
            {
                newPrompt = stealthKill.Prompt;
            }
            else if (_current != null && _current.CanInteract())
            {
                newPrompt = _current.GetPrompt();
            }

            if (newPrompt != CurrentPrompt)
            {
                CurrentPrompt = newPrompt;
                OnPromptChanged?.Invoke(CurrentPrompt);
            }
        }

        private void OnDisable()
        {
            _current?.OnLoseFocus();
            _current = null;
            CurrentPrompt = string.Empty;
            OnPromptChanged?.Invoke(string.Empty);
        }
    }
}