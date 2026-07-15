using UnityEngine;
using TheForest.Interaction;

namespace TheForest.Building
{
    /// <summary>
    /// Khúc gỗ tồn tại NGOÀI THẾ GIỚI (world object) — KHÔNG phải inventory item.
    /// Gắn lên prefab log có Rigidbody + Collider (khuyên dùng CapsuleCollider nằm ngang,
    /// rẻ hơn MeshCollider convex).
    /// Nhặt qua IInteractable -> PlayerLogCarry (tối đa 2 log).
    /// Khi trở thành một phần công trình (IsPartOfStructure) -> immutable theo luật sole-support.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class WorldLog : MonoBehaviour, IInteractable
    {
        [Header("Kiểu cắt")]
        [SerializeField] private LogCutType cutType = LogCutType.Full;

        public LogCutType CutType => cutType;

        /// <summary>Đang nằm trên tay người chơi.</summary>
        public bool IsCarried { get; private set; }

        /// <summary>
        /// Log đã là một phần của công trình -> không nhặt/gỡ trực tiếp.
        /// Structural integrity graph (sole-support check) sẽ quyết định khi nào được phép gỡ.
        /// </summary>
        public bool IsPartOfStructure { get; set; }

        /// <summary>Plank không bao giờ được đặt đứng (luật SOTF).</summary>
        public bool CanBePlacedVertically =>
            cutType == LogCutType.Full ||
            cutType == LogCutType.ThreeQuarter ||
            cutType == LogCutType.Half ||
            cutType == LogCutType.Quarter;

        private Rigidbody _rb;
        private Collider[] _colliders;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>();
        }

        /// <summary>Đổi kiểu cắt (dùng bởi LogCutController khi split log sau này).</summary>
        public void SetCutType(LogCutType type)
        {
            cutType = type;
        }

        /// <summary>Bật/tắt physics khi được vác lên tay hoặc đặt vào công trình.</summary>
        public void SetPhysicsEnabled(bool physicsEnabled)
        {
            if (_rb != null)
            {
                _rb.isKinematic = !physicsEnabled;
                if (physicsEnabled) _rb.WakeUp();
            }

            if (_colliders != null)
            {
                foreach (var col in _colliders)
                {
                    if (col != null) col.enabled = physicsEnabled;
                }
            }
        }

        /// <summary>PlayerLogCarry gọi khi nhặt: tắt physics, gắn lên carry anchor.</summary>
        public void OnPickedUp(Transform carryAnchor, Vector3 localOffset)
        {
            IsCarried = true;
            SetPhysicsEnabled(false);
            transform.SetParent(carryAnchor, false);
            transform.localPosition = localOffset;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>PlayerLogCarry gọi khi thả/ném: trả về thế giới, bật lại physics.</summary>
        public void OnDropped(Vector3 worldPosition, Quaternion worldRotation)
        {
            IsCarried = false;
            transform.SetParent(null);
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            SetPhysicsEnabled(true);
        }

        // ===================== IInteractable =====================
        public string GetPrompt()
        {
            return $"Vác {GetDisplayName(cutType)}";
        }

        public bool CanInteract()
        {
            return !IsCarried && !IsPartOfStructure;
        }

        public void Interact(GameObject interactor)
        {
            var carry = interactor.GetComponent<TheForest.Player.PlayerLogCarry>();
            if (carry == null)
            {
                Debug.LogWarning("[WorldLog] Người tương tác không có PlayerLogCarry.");
                return;
            }

            carry.TryPickUp(this);
        }

        public void OnFocus() { /* TODO: outline highlight */ }
        public void OnLoseFocus() { /* TODO: tắt outline */ }

        // ===================== Helpers =====================
        public static string GetDisplayName(LogCutType type)
        {
            switch (type)
            {
                case LogCutType.Full: return "Khúc gỗ";
                case LogCutType.ThreeQuarter: return "Khúc gỗ 3/4";
                case LogCutType.Half: return "Nửa khúc gỗ";
                case LogCutType.Quarter: return "1/4 khúc gỗ";
                case LogCutType.Plank: return "Tấm ván";
                case LogCutType.PlankThreeQuarter: return "Tấm ván 3/4";
                case LogCutType.PlankHalf: return "Nửa tấm ván";
                case LogCutType.PlankQuarter: return "1/4 tấm ván";
                case LogCutType.Firewood: return "Củi đốt";
                default: return "Gỗ";
            }
        }
    }
}
