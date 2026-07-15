using System;
using UnityEngine;
using TheForest.Interaction;
using TheForest.Player;
using TheForest.World;

namespace TheForest.Building
{
    /// <summary>
    /// Giường ngủ dùng CHUNG runtime cho cả Stick Bed và Double Bed (GDD: 2 loại chỉ khác nguyên liệu
    /// craft — 16 Sticks + 1 Duct Tape vs 40 Sticks + 4 Logs + 2 Duct Tape + 2 Animal Hides — nguyên liệu
    /// đó xử lý ở tầng BlueprintData/BlueprintSystem khi DỰNG giường, không liên quan runtime này).
    ///
    /// GDD quan trọng: Sleep và Save là HAI hành động TÁCH BIỆT, không tự gắn với nhau.
    ///   [E]  = Save (qua IInteractable.Interact, giống mọi tương tác khác trong dự án)
    ///   [Z]  = Sleep (không đi qua IInteractable — xem PlayerBedInteraction, theo đúng pattern
    ///          StealthKillController đã có: raycast + input riêng trên Player, ưu tiên trước/độc lập
    ///          với InteractionRaycaster).
    ///
    /// Sleep yêu cầu Energy đủ thấp (trừ maxEnergyToSleep) và dùng SafeSleepChecker để xác định có bị
    /// cannibal quấy rối hay không nếu ngủ ở nơi không an toàn.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BedController : MonoBehaviour, IInteractable
    {
        [Header("Tham chiếu")]
        [SerializeField] private DayNightCycle dayNight;
        [SerializeField] private SafeSleepChecker safeSleepChecker;

        [Header("Cấu hình ngủ")]
        [Tooltip("Ngủ ban ĐÊM -> thức dậy giờ này (theo GDD: qua bình minh).")]
        [SerializeField] private float wakeHourFromNight = 7f;
        [Tooltip("Ngủ ban NGÀY -> thức dậy giờ này (GDD: ngủ ngày dậy đêm).")]
        [SerializeField] private float wakeHourFromDay = 20f;
        [Tooltip("Ngưỡng Energy TỐI ĐA để được phép ngủ (GDD: phải đủ mệt mới ngủ được).")]
        [Range(0f, 1f)][SerializeField] private float maxEnergyToSleep = 0.7f;
        [SerializeField] private float sleepHealthRestore = 15f;
        [SerializeField] private float sleepEnergyRestore = 60f;

        public event Action OnSaveRequested;
        public event Action OnSleepStarted;
        public event Action<int> OnSleepEnded; // int = số lần bị quấy rối (0 nếu an toàn/không bị)

        private bool _isFocused;

        private void Awake()
        {
            if (dayNight == null) dayNight = FindFirstObjectByType<DayNightCycle>();
            if (safeSleepChecker == null) safeSleepChecker = GetComponentInChildren<SafeSleepChecker>();
        }

        // ===================== SAVE (phím E qua IInteractable) =====================
        public void RequestSave()
        {
            OnSaveRequested?.Invoke();
            Debug.Log("[Bed] Đã lưu game tại giường.");
        }

        // ===================== SLEEP (phím Z, gọi từ PlayerBedInteraction) =====================

        /// <summary>true nếu hiện tại đủ điều kiện Energy để ngủ (dùng cho UI hiển thị prompt trước khi thử ngủ).</summary>
        public bool CanSleepNow(SurvivalStats stats) => stats != null && stats.EnergyNormalized <= maxEnergyToSleep;

        public bool TrySleep(SurvivalStats stats)
        {
            if (stats == null || dayNight == null) return false;

            if (stats.EnergyNormalized > maxEnergyToSleep)
            {
                Debug.Log("[Bed] Chưa đủ mệt để ngủ.");
                return false;
            }

            OnSleepStarted?.Invoke();

            bool wasNight = dayNight.IsNight;
            float targetHour = wasNight ? wakeHourFromNight : wakeHourFromDay;
            dayNight.SkipToHour(targetHour);

            stats.Sleep(sleepHealthRestore, sleepEnergyRestore);

            bool isSafe = safeSleepChecker == null || safeSleepChecker.IsSafeNow;
            int interruptions = 0;
            if (!isSafe && safeSleepChecker != null)
            {
                interruptions = safeSleepChecker.RollNightInterruptions();
                if (interruptions > 0)
                    Debug.Log($"[Bed] Giấc ngủ bị quấy rối {interruptions} lần bởi cannibal (ngủ nơi không an toàn).");
            }

            OnSleepEnded?.Invoke(interruptions);
            return true;
        }

        // ===================== IInteractable (E = Save) =====================
        public string GetPrompt() => "[E] Lưu game   —   [Z] Ngủ";
        public bool CanInteract() => true;
        public void Interact(GameObject interactor) => RequestSave();
        public void OnFocus() { _isFocused = true; }
        public void OnLoseFocus() { _isFocused = false; }
    }
}
