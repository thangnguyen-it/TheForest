using System;
using UnityEngine;

namespace TheForest.World
{
    /// <summary>
    /// Trạng thái mưa toàn cục — hạ tầng CÒN THIẾU mà Block 5 (Rain Catcher) cần, đồng thời lấp đúng
    /// khoảng trống mà SurvivalStats.cs đã tự chừa sẵn ("cờ ngoài cấp vào (set bởi PlayerController,
    /// WeatherSystem...)" — comment gốc trên field IsWet — nhưng WeatherSystem chưa từng được viết).
    ///
    /// Kiến trúc: singleton + event tĩnh, cùng tinh thần với NoiseSystem/FireRegistry đã có trong dự án
    /// (đơn giản, không cần SO channel riêng). autoCycle cho phép random mưa/tạnh theo giờ game (qua
    /// DayNightCycle.OnHourChanged) để test nhanh KHÔNG cần hệ thống thời tiết đầy đủ; có thể tắt
    /// autoCycle và để một hệ thống scripted-event khác gọi SetRaining() trực tiếp khi cần.
    ///
    /// LƯU Ý PHẠM VI: WeatherSystem CHƯA tự set SurvivalStats.IsWet (việc đó cần thêm kiểm tra
    /// "player có đang ở dưới mái che không" dựa trên ShelterStatusChangedEvent của Building System —
    /// không nằm trong Block 5, cố tình để ngỏ làm follow-up tránh set ướt nhầm cho người chơi trong nhà).
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }

        [Header("Mưa ngẫu nhiên (tuỳ chọn - tắt nếu có hệ thống thời tiết khác điều khiển qua SetRaining)")]
        [SerializeField] private bool autoCycle = true;
        [SerializeField] private float minClearHours = 2f;
        [SerializeField] private float maxClearHours = 6f;
        [SerializeField] private float minRainHours = 0.5f;
        [SerializeField] private float maxRainHours = 2f;

        [Header("Tham chiếu")]
        [SerializeField] private DayNightCycle dayNight;

        public bool IsRaining { get; private set; }

        /// <summary>Sự kiện tĩnh: mọi hệ thống (RainCatcher, HUD mưa, tương lai IsWet...) subscribe từ đây.</summary>
        public static event Action<bool> OnRainChanged;

        private float _phaseTimerHours;
        private float _phaseDurationHours;
        private float _lastHour = -1f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (dayNight == null) dayNight = FindFirstObjectByType<DayNightCycle>();
            RollNextPhaseDuration();
        }

        private void OnEnable()
        {
            if (dayNight != null) dayNight.OnHourChanged += HandleHourChanged;
        }

        private void OnDisable()
        {
            if (dayNight != null) dayNight.OnHourChanged -= HandleHourChanged;
            if (Instance == this) Instance = null;
        }

        private void HandleHourChanged(float currentHour)
        {
            if (!autoCycle) return;

            if (_lastHour < 0f) { _lastHour = currentHour; return; }

            float delta = currentHour - _lastHour;
            if (delta < 0f) delta += 24f; // qua nửa đêm
            _lastHour = currentHour;

            _phaseTimerHours += delta;
            if (_phaseTimerHours >= _phaseDurationHours)
            {
                _phaseTimerHours = 0f;
                SetRaining(!IsRaining);
                RollNextPhaseDuration();
            }
        }

        private void RollNextPhaseDuration()
        {
            _phaseDurationHours = IsRaining
                ? UnityEngine.Random.Range(minRainHours, maxRainHours)
                : UnityEngine.Random.Range(minClearHours, maxClearHours);
        }

        /// <summary>Cho phép hệ thống ngoài (scripted event, debug console...) ép trạng thái mưa trực tiếp.</summary>
        public void SetRaining(bool raining)
        {
            if (IsRaining == raining) return;
            IsRaining = raining;
            OnRainChanged?.Invoke(IsRaining);
        }
    }
}
