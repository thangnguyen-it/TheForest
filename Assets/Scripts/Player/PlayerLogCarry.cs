using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TheForest.Building;

namespace TheForest.Player
{
    /// <summary>
    /// Hệ "vác gỗ" song song với Inventory: log là world object, KHÔNG vào túi đồ.
    /// - Tối đa 2 log (không có upgrade — constraint design của SOTF).
    /// - Loại trừ lẫn nhau với EquipmentController: vác gỗ thì tự cất vũ khí, rút vũ khí thì tự thả gỗ.
    /// - Phím G để thả log (fallback poll; có thể map action "DropLog" trong Input Actions rồi bỏ fallback).
    /// </summary>
    public class PlayerLogCarry : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [Tooltip("Điểm gắn log trên vai/tay player (con của Camera hoặc thân player).")]
        [SerializeField] private Transform carryAnchor;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private EquipmentController equipment;

        [Header("Cấu hình")]
        [SerializeField] private int maxLogs = 2;
        [Tooltip("Khoảng cách trước mặt khi thả log xuống.")]
        [SerializeField] private float dropForwardDistance = 1.2f;
        [Tooltip("Lực ném log (gây stagger kẻ địch, săn thú nhỏ).")]
        [SerializeField] private float throwForce = 8f;
        [Tooltip("Offset local của log thứ 2 trên anchor để không chồng lên log thứ nhất.")]
        [SerializeField] private Vector3 secondLogOffset = new Vector3(0.25f, 0f, 0f);

        private readonly List<WorldLog> _carried = new List<WorldLog>();

        public int CarriedCount => _carried.Count;
        public bool IsCarrying => _carried.Count > 0;
        public bool HasFreeSlot => _carried.Count < maxLogs;

        /// <summary>Bắn khi số log đang vác thay đổi (cho HUD).</summary>
        public event Action<int> OnCarryChanged;

        private void Awake()
        {
            if (equipment == null) equipment = GetComponent<EquipmentController>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        private void OnEnable()
        {
            if (equipment != null) equipment.OnEquippedChanged += HandleEquippedChanged;
        }

        private void OnDisable()
        {
            if (equipment != null) equipment.OnEquippedChanged -= HandleEquippedChanged;
        }

        private void Update()
        {
            // Fallback phím G khi chưa khai báo action "DropLog" trong Input Actions asset.
            // Nếu đã map OnDropLog qua PlayerInput thì xóa đoạn này để tránh trigger đôi.
            if (IsCarrying && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
            {
                DropOne();
            }
        }

        /// <summary>Input System message: map action "DropLog" (phím G) trong Input Actions asset.</summary>
        public void OnDropLog(InputValue value)
        {
            if (value.isPressed) DropOne();
        }

        /// <summary>Loại trừ lẫn nhau: rút vũ khí ra thì thả hết gỗ đang vác.</summary>
        private void HandleEquippedChanged(Items.ItemData item)
        {
            if (item != null && IsCarrying) DropAll();
        }

        /// <summary>WorldLog.Interact gọi khi player nhấn E vào log dưới đất.</summary>
        public bool TryPickUp(WorldLog log)
        {
            if (log == null || log.IsCarried || log.IsPartOfStructure) return false;
            if (!HasFreeSlot) return false;

            if (carryAnchor == null)
            {
                Debug.LogWarning("[PlayerLogCarry] Chưa gán carryAnchor — không thể vác log.");
                return false;
            }

            // Vác gỗ thì cất vũ khí (mutual exclusion chiều ngược lại)
            if (equipment != null && equipment.HasEquipped) equipment.Unequip();

            Vector3 offset = _carried.Count == 0 ? Vector3.zero : secondLogOffset;
            log.OnPickedUp(carryAnchor, offset);
            _carried.Add(log);

            OnCarryChanged?.Invoke(_carried.Count);
            return true;
        }

        /// <summary>Thả log trên cùng xuống trước mặt (phím G).</summary>
        public void DropOne()
        {
            if (!IsCarrying) return;

            WorldLog log = _carried[_carried.Count - 1];
            _carried.RemoveAt(_carried.Count - 1);
            ReleaseToWorld(log, Vector3.zero);

            OnCarryChanged?.Invoke(_carried.Count);
        }

        /// <summary>Ném log theo hướng nhìn (stagger kẻ địch, săn thú nhỏ).</summary>
        public void ThrowOne()
        {
            if (!IsCarrying) return;

            WorldLog log = _carried[_carried.Count - 1];
            _carried.RemoveAt(_carried.Count - 1);

            Vector3 forward = cameraTransform != null ? cameraTransform.forward : transform.forward;
            ReleaseToWorld(log, forward * throwForce);

            OnCarryChanged?.Invoke(_carried.Count);
        }

        public void DropAll()
        {
            while (IsCarrying) DropOne();
        }

        /// <summary>
        /// Tiêu thụ log trên cùng (Building System gọi khi log được đặt vào công trình).
        /// Trả về WorldLog đã tách khỏi tay, KHÔNG bật lại physics — caller tự quyết định.
        /// </summary>
        public WorldLog ConsumeOne()
        {
            if (!IsCarrying) return null;

            WorldLog log = _carried[_carried.Count - 1];
            _carried.RemoveAt(_carried.Count - 1);
            log.transform.SetParent(null);

            OnCarryChanged?.Invoke(_carried.Count);
            return log;
        }

        private void ReleaseToWorld(WorldLog log, Vector3 impulse)
        {
            Vector3 forward = cameraTransform != null ? cameraTransform.forward : transform.forward;
            Vector3 flatForward = new Vector3(forward.x, 0f, forward.z).normalized;
            if (flatForward.sqrMagnitude < 0.001f) flatForward = transform.forward;

            Vector3 pos = transform.position + flatForward * dropForwardDistance + Vector3.up * 0.8f;
            Quaternion rot = Quaternion.LookRotation(flatForward);

            log.OnDropped(pos, rot);

            if (impulse != Vector3.zero)
            {
                var rb = log.GetComponent<Rigidbody>();
                if (rb != null) rb.AddForce(impulse, ForceMode.Impulse);
            }
        }
    }
}
