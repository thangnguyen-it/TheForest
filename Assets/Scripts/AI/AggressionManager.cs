using System;
using System.Collections.Generic;
using UnityEngine;
using TheForest.World;
using Companion.Events;

namespace TheForest.AI
{
    /// <summary>
    /// Quản lý độ hung hãn theo GDD:
    /// - Global: tự tăng theo số ngày sống sót, KHÔNG có cách chủ động làm giảm.
    /// - Local : theo từng vùng (zone), tăng khi player gây hấn, tự nguội dần.
    /// - Unlock loại địch theo mốc ngày (Pale 5-6, Creepy 7, Masked 22).
    /// CannibalAI hỏi GetEffectiveAggression(zoneId) để scale phát hiện/tấn công.
    /// </summary>
    public class AggressionManager : MonoBehaviour
    {
        public static AggressionManager Instance { get; private set; }

        [Header("Tham chiếu")]
        [SerializeField] private DayNightCycle dayNight;

        [Header("Event Channel (Bus)")]
        [SerializeField] private AggressionEventChannelSO aggressionChannel;

        [Header("Global aggression (theo ngày)")]
        [Tooltip("Giá trị global ngày 1.")]
        [SerializeField] private float globalBase = 1f;
        [Tooltip("Cộng thêm mỗi ngày trôi qua.")]
        [SerializeField] private float globalPerDay = 0.12f;
        [Tooltip("Trần global (tránh vô hạn).")]
        [SerializeField] private float globalMax = 3f;

        [Header("Local aggression (theo vùng)")]
        [Tooltip("Local nguội bao nhiêu mỗi giây (về 0).")]
        [SerializeField] private float localDecayPerSec = 0.02f;
        [Tooltip("Trần local mỗi vùng.")]
        [SerializeField] private float localMax = 2f;

        [Header("Mốc unlock loại địch (số ngày)")]
        [SerializeField] private int paleSkinnyDay = 5;
        [SerializeField] private int paleDay = 6;
        [SerializeField] private int creepyDay = 7;
        [SerializeField] private int maskedDay = 22;

        // ===================== RUNTIME =====================
        public float GlobalAggression { get; private set; }
        public int CurrentDay { get; private set; } = 1;

        // local theo zoneId (int). zone 0 = mặc định/toàn đảo.
        private readonly Dictionary<int, float> _localByZone = new Dictionary<int, float>();

        public event Action<float> OnGlobalAggressionChanged; // cho debug/UI
        public event Action<int> OnEnemyTierUnlocked;         // (day) khi qua mốc

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            GlobalAggression = globalBase;
        }

        private void OnEnable()
        {
            if (dayNight != null) dayNight.OnNewDay += HandleNewDay;
            if (aggressionChannel != null) aggressionChannel.Register(OnAggressionDelta);
        }

        private void OnDisable()
        {
            if (dayNight != null) dayNight.OnNewDay -= HandleNewDay;
            if (aggressionChannel != null) aggressionChannel.Unregister(OnAggressionDelta);
            if (Instance == this) Instance = null;
        }

        private void OnAggressionDelta(AggressionDelta d)
        {
            AddLocalAggression(d.ZoneId, d.Amount);
        }

        private void Update()
        {
            if (_localByZone.Count == 0) return;

            // Local tự nguội dần (GDD: local phản ánh hành động gần đây)
            float dec = localDecayPerSec * Time.deltaTime;
            // copy keys để sửa trong vòng lặp
            var keys = new List<int>(_localByZone.Keys);
            foreach (var k in keys)
            {
                float v = _localByZone[k] - dec;
                _localByZone[k] = Mathf.Max(0f, v);
            }
        }

        // ===================== GLOBAL THEO NGÀY =====================
        private void HandleNewDay(int day)
        {
            CurrentDay = day;

            float prev = GlobalAggression;
            GlobalAggression = Mathf.Min(globalMax, globalBase + globalPerDay * (day - 1));
            if (!Mathf.Approximately(prev, GlobalAggression))
                OnGlobalAggressionChanged?.Invoke(GlobalAggression);

            // Báo unlock khi vừa qua mốc
            if (day == paleSkinnyDay || day == paleDay || day == creepyDay || day == maskedDay)
                OnEnemyTierUnlocked?.Invoke(day);
        }

        // ===================== LOCAL THEO VÙNG =====================

        /// <summary>Player gây hấn trong vùng (giết cannibal, đốt làng...) -> tăng local.</summary>
        public void AddLocalAggression(int zoneId, float amount)
        {
            _localByZone.TryGetValue(zoneId, out float cur);
            _localByZone[zoneId] = Mathf.Clamp(cur + amount, 0f, localMax);
        }

        public float GetLocalAggression(int zoneId)
        {
            _localByZone.TryGetValue(zoneId, out float v);
            return v;
        }

        /// <summary>Tổng hung hãn hiệu dụng cho 1 AI ở vùng zoneId.</summary>
        public float GetEffectiveAggression(int zoneId)
        {
            return GlobalAggression + GetLocalAggression(zoneId);
        }

        // ===================== UNLOCK LOẠI ĐỊCH =====================
        public bool IsPaleSkinnyUnlocked => CurrentDay >= paleSkinnyDay;
        public bool IsPaleUnlocked => CurrentDay >= paleDay;
        public bool IsCreepyUnlocked => CurrentDay >= creepyDay;
        public bool IsMaskedUnlocked => CurrentDay >= maskedDay;

        /// <summary>Loại tribe cao nhất được phép spawn theo ngày (cho spawner sau này).</summary>
        public CannibalTribe HighestUnlockedTribe()
        {
            if (IsMaskedUnlocked) return CannibalTribe.Masked;
            if (IsPaleUnlocked) return CannibalTribe.Pale;
            if (IsPaleSkinnyUnlocked) return CannibalTribe.PaleSkinny;
            return CannibalTribe.Regular;
        }
    }
}