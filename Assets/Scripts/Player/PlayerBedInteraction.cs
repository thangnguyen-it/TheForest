using UnityEngine;
using UnityEngine.InputSystem;
using TheForest.Building;

namespace TheForest.Player
{
    /// <summary>
    /// Phát hiện giường đang nhìn vào để xử lý phím Z = Ngủ, TÁCH BIỆT với phím E = Tương tác/Save
    /// (BedController.Interact đã xử lý E qua InteractionRaycaster như bình thường).
    ///
    /// Thiết kế theo ĐÚNG pattern đã có của StealthKillController: raycast RIÊNG trên Player thay vì
    /// dùng lại _current (private) của InteractionRaycaster — giữ nhất quán kiến trúc hiện có, tránh
    /// phải đổi InteractionRaycaster thành kiểu public-expose-target chỉ để phục vụ 1 tính năng phụ.
    ///
    /// Gắn lên Player cạnh InteractionRaycaster/StealthKillController. Cần thêm action Input System
    /// tên "Sleep" (map phím Z) trong Input Actions asset, gọi tới OnSleep(InputValue).
    /// </summary>
    public class PlayerBedInteraction : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private SurvivalStats stats;

        [Header("Raycast")]
        [SerializeField] private float range = 3f;
        [SerializeField] private LayerMask bedMask = ~0;

        private BedController _currentBed;

        public bool HasBedTarget => _currentBed != null;
        public string SleepPrompt => _currentBed != null && _currentBed.CanSleepNow(stats) ? "[Z] Ngủ" : string.Empty;

        private void Awake()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (stats == null) stats = GetComponent<SurvivalStats>();
        }

        private void Update()
        {
            _currentBed = null;
            if (cameraTransform == null) return;

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, range, bedMask, QueryTriggerInteraction.Ignore))
                _currentBed = hit.collider.GetComponentInParent<BedController>();
        }

        /// <summary>Input System message: map action "Sleep" (phím Z) trong Input Actions asset.</summary>
        public void OnSleep(InputValue value)
        {
            if (!value.isPressed || _currentBed == null) return;
            _currentBed.TrySleep(stats);
        }
    }
}
