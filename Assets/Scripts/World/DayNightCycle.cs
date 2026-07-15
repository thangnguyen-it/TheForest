using System;
using UnityEngine;

namespace TheForest.World
{
    /// <summary>
    /// Chu kỳ ngày/đêm. Giữ thời gian trong game (0..24h), xoay mặt trời,
    /// phát event khi chuyển ngày/đêm và khi sang ngày mới.
    /// Các hệ thống khác (tree regrow, AI aggression theo ngày, sleep) lắng nghe qua event.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [Tooltip("Directional Light đóng vai mặt trời (Sun trong HDRP)")]
        [SerializeField] private Light sunLight;

        [Header("Thời gian")]
        [Tooltip("Số phút thực = 1 ngày trong game")]
        [SerializeField] private float minutesPerFullDay = 20f;
        [Tooltip("Giờ bắt đầu khi load (0..24). Tai nạn máy bay ~ buổi sáng.")]
        [SerializeField, Range(0f, 24f)] private float startHour = 8f;

        [Header("Mốc ngày/đêm (giờ)")]
        [SerializeField, Range(0f, 24f)] private float dawnHour = 6f;   // bình minh
        [SerializeField, Range(0f, 24f)] private float duskHour = 19f;  // hoàng hôn

        [Header("Cường độ ánh sáng")]
        [SerializeField] private float dayIntensity = 1.2f;
        [SerializeField] private float nightIntensity = 0.05f;
        [SerializeField] private float transitionSharpness = 3f;

        // ===================== TRẠNG THÁI =====================
        public float CurrentHour { get; private set; }   // 0..24
        public int DayNumber { get; private set; } = 1;
        public bool IsNight { get; private set; }

        private float _dayLengthSeconds;
        private bool _initialized;

        // ===================== EVENT =====================
        public event Action<int> OnNewDay;      // (số ngày mới) — dùng cho AI global aggression tăng dần
        public event Action OnDayStart;    // bắt đầu ban ngày (qua dawn)
        public event Action OnNightStart;  // bắt đầu ban đêm (qua dusk)
        public event Action<float> OnHourChanged; // (giờ hiện tại) — cho UI đồng hồ nếu cần

        private void Start()
        {
            _dayLengthSeconds = minutesPerFullDay * 60f;
            CurrentHour = startHour;
            IsNight = CurrentHour < dawnHour || CurrentHour >= duskHour;
            _initialized = true;
            UpdateSun();
        }

        private void Update()
        {
            if (!_initialized) return;

            // Tiến thời gian: 24h trải đều trong _dayLengthSeconds
            float hoursPerSecond = 24f / _dayLengthSeconds;
            float prevHour = CurrentHour;
            CurrentHour += hoursPerSecond * Time.deltaTime;

            // Sang ngày mới
            if (CurrentHour >= 24f)
            {
                CurrentHour -= 24f;
                DayNumber++;
                OnNewDay?.Invoke(DayNumber);
            }

            OnHourChanged?.Invoke(CurrentHour);
            CheckDayNightTransition(prevHour);
            UpdateSun();
        }

        private void CheckDayNightTransition(float prevHour)
        {
            bool wasNight = IsNight;
            IsNight = CurrentHour < dawnHour || CurrentHour >= duskHour;

            if (wasNight && !IsNight) OnDayStart?.Invoke();
            else if (!wasNight && IsNight) OnNightStart?.Invoke();
        }

        private void UpdateSun()
        {
            if (sunLight == null) return;

            // Xoay mặt trời: 0h = -90° (dưới chân trời), 6h = 0° (mọc), 12h = 90° (đỉnh), 18h = 180°
            float sunAngle = (CurrentHour / 24f) * 360f - 90f;
            sunLight.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);

            // Cường độ: ban ngày sáng, ban đêm tối, chuyển mượt quanh dawn/dusk
            float dayFactor = ComputeDayFactor();
            sunLight.intensity = Mathf.Lerp(nightIntensity, dayIntensity, dayFactor);
        }

        /// <summary>Trả 1 lúc giữa ngày, 0 lúc giữa đêm, mượt ở giao thời.</summary>
        private float ComputeDayFactor()
        {
            // Khoảng cách "vào ban ngày": dùng sin theo vị trí mặt trời cho mượt
            float t = Mathf.Sin((CurrentHour / 24f) * Mathf.PI * 2f - Mathf.PI / 2f);
            // t: -1 (nửa đêm) .. 1 (giữa trưa). Đưa về 0..1 và làm gắt hơn quanh giao thời.
            float normalized = (t + 1f) * 0.5f;
            return Mathf.Clamp01(Mathf.Pow(normalized, 1f / transitionSharpness));
        }

        // ===================== API CÔNG KHAI =====================

        /// <summary>
        /// Ngủ tới một giờ mục tiêu (vd ngủ tới sáng). Tua nhanh thời gian,
        /// xử lý sang ngày mới nếu vượt qua nửa đêm. Gọi từ Shelter/giường.
        /// </summary>
        public void SkipToHour(float targetHour)
        {
            targetHour = Mathf.Repeat(targetHour, 24f);
            if (targetHour <= CurrentHour) // qua nửa đêm => sang ngày mới
            {
                DayNumber++;
                OnNewDay?.Invoke(DayNumber);
            }
            CurrentHour = targetHour;
            CheckDayNightTransition(CurrentHour);
            UpdateSun();
        }

        /// <summary>Restores world time without simulating every skipped day.</summary>
        public void RestoreTime(int dayNumber, float hour)
        {
            DayNumber = Mathf.Max(1, dayNumber);
            CurrentHour = Mathf.Repeat(hour, 24f);
            _dayLengthSeconds = Mathf.Max(1f, minutesPerFullDay * 60f);
            _initialized = true;
            IsNight = CurrentHour < dawnHour || CurrentHour >= duskHour;
            OnHourChanged?.Invoke(CurrentHour);
            UpdateSun();
        }
    }
}
