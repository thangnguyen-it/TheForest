using UnityEngine;
using Unity.Netcode;
using TheForest.Interaction;
using TheForest.Items;
using TheForest.Multiplayer;
using TheForest.Player;
using TheForest.World;
using TheForest.Persistence;

namespace TheForest.Building
{
    /// <summary>
    /// Hứng nước mưa thụ động theo GDD (Block 5): 1 phút mưa (giờ game, theo DayNightCycle) = 1 đơn vị
    /// nước sạch (bằng 1 lần refill Printed Flask). Bay hơi ~1 mức/ngày khi hạn hán (không mưa cả ngày).
    /// Nước trong Rain Catcher là nước SẠCH — uống trực tiếp không cần đun sôi (khác Cooking Pot).
    ///
    /// Nguyên liệu craft (12 Sticks + 1 Turtle Shell — CHỈ Sea Turtle, không phải rùa nước ngọt) xử lý
    /// ở tầng BlueprintData khi dựng, không thuộc runtime này.
    ///
    /// Phụ thuộc WeatherSystem (Block 5, file WeatherSystem.cs) làm nguồn "đang mưa hay không".
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class RainCatcher : NetworkBehaviour, IInteractable, IPersistentStateParticipant
    {
        [Header("Sức chứa")]
        [Tooltip("Đơn vị nước tối đa (1 đơn vị = 1 lần refill Printed Flask / 1 lần uống).")]
        [SerializeField] private float maxWaterUnits = 6f;
        [SerializeField] private float currentWaterUnits = 0f;

        [Header("Tốc độ")]
        [SerializeField] private float unitsPerMinuteRaining = 1f;
        [Tooltip("Bay hơi mỗi NGÀY khi hạn hán (không mưa suốt ngày đó).")]
        [SerializeField] private float evaporationPerDay = 1f;
        [Tooltip("Lượng thirst hồi mỗi lần uống trực tiếp 1 đơn vị nước.")]
        [SerializeField] private float thirstRestorePerUnit = 40f;

        [Header("Đổ đầy Flask (tuỳ chọn)")]
        [Tooltip("Item nước sạch cấp vào túi khi TryFillFlask thành công (để trống nếu chưa cần tính năng này).")]
        [SerializeField] private ItemData cleanWaterItem;

        [Header("Visual")]
        [Tooltip("Object hiển thị mực nước, sẽ bị scale.y theo WaterNormalized (tuỳ chọn).")]
        [SerializeField] private GameObject waterVisual;

        private DayNightCycle _dayNight;
        private float _lastHour = -1f;
        private string _persistenceId;
        private NetworkWorldObjectState _networkState;

        public float WaterNormalized => currentWaterUnits / Mathf.Max(0.01f, maxWaterUnits);
        public bool HasWater => currentWaterUnits >= 1f;
        public string PersistenceId => string.IsNullOrEmpty(_persistenceId)
            ? _persistenceId = PersistentStateId.For(this)
            : _persistenceId;

        [System.Serializable]
        private sealed class RainCatcherState
        {
            public float waterUnits;
            public float lastHour;
        }

        private void Awake()
        {
            _persistenceId = PersistentStateId.For(this);
            _networkState = GetComponent<NetworkWorldObjectState>();
            _dayNight = FindFirstObjectByType<DayNightCycle>();
            ApplyVisual();
        }

        private void OnEnable()
        {
            if (_dayNight != null)
            {
                _dayNight.OnHourChanged += HandleHourChanged;
                _dayNight.OnNewDay += HandleNewDay;
            }
        }

        private void OnDisable()
        {
            if (_dayNight != null)
            {
                _dayNight.OnHourChanged -= HandleHourChanged;
                _dayNight.OnNewDay -= HandleNewDay;
            }
        }

        private bool IsRainingNow => WeatherSystem.Instance != null && WeatherSystem.Instance.IsRaining;

        private void HandleHourChanged(float currentHour)
        {
            if (!NetworkWorldInteraction.ShouldSimulateHere(this)) return;
            if (!IsRainingNow) { _lastHour = currentHour; return; }
            if (_lastHour < 0f) { _lastHour = currentHour; return; }

            float deltaHours = currentHour - _lastHour;
            if (deltaHours < 0f) deltaHours += 24f; // qua nửa đêm
            _lastHour = currentHour;

            float minutes = deltaHours * 60f;
            AddWater(minutes * unitsPerMinuteRaining);
        }

        private void HandleNewDay(int day)
        {
            if (!NetworkWorldInteraction.ShouldSimulateHere(this)) return;
            if (!IsRainingNow) AddWater(-evaporationPerDay);
        }

        private void AddWater(float delta)
        {
            currentWaterUnits = Mathf.Clamp(currentWaterUnits + delta, 0f, maxWaterUnits);
            ApplyVisual();
            PublishNetworkState();
        }

        /// <summary>Uống trực tiếp tại chỗ (đầy 1 đơn vị/lần), không cần đun sôi.</summary>
        public bool TryDrink(SurvivalStats stats)
        {
            if (NetworkWorldInteraction.ShouldRouteToServer(this))
            {
                TryDrinkLocal(stats);
                DrinkServerRpc();
                return true;
            }

            return TryDrinkLocal(stats);
        }

        private bool TryDrinkLocal(SurvivalStats stats)
        {
            if (stats == null || currentWaterUnits < 1f) return false;
            currentWaterUnits -= 1f;
            stats.Drink(thirstRestorePerUnit);
            ApplyVisual();
            PublishNetworkState();
            return true;
        }

        /// <summary>Đổ đầy 1 Printed Flask (cấp cleanWaterItem vào túi) nếu đã cấu hình item nước sạch.</summary>
        public bool TryFillFlask(Inventory inv)
        {
            if (NetworkWorldInteraction.ShouldRouteToServer(this))
            {
                TryFillFlaskLocal(inv);
                FillFlaskServerRpc();
                return true;
            }

            return TryFillFlaskLocal(inv);
        }

        private bool TryFillFlaskLocal(Inventory inv)
        {
            if (inv == null || cleanWaterItem == null || currentWaterUnits < 1f) return false;

            int leftover = inv.Add(cleanWaterItem, 1);
            if (leftover > 0) return false; // túi đầy, không tiêu nước

            currentWaterUnits -= 1f;
            ApplyVisual();
            PublishNetworkState();
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void DrinkServerRpc(RpcParams rpcParams = default)
        {
            if (!NetworkWorldInteraction.TryGetPlayerObject(rpcParams.Receive.SenderClientId, out GameObject playerObject)) return;
            TryDrinkLocal(playerObject.GetComponent<SurvivalStats>());
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void FillFlaskServerRpc(RpcParams rpcParams = default)
        {
            if (!NetworkWorldInteraction.TryGetPlayerObject(rpcParams.Receive.SenderClientId, out GameObject playerObject)) return;
            TryFillFlaskLocal(playerObject.GetComponent<Inventory>());
        }

        private void ApplyVisual()
        {
            if (waterVisual == null) return;
            var s = waterVisual.transform.localScale;
            waterVisual.transform.localScale = new Vector3(s.x, Mathf.Max(0.02f, WaterNormalized), s.z);
        }

        // ===================== IInteractable =====================
        public string GetPrompt() => HasWater ? "[E] Uống nước mưa" : "Hứng nước mưa (trống)";
        public bool CanInteract() => true;

        public void Interact(GameObject interactor)
        {
            var stats = interactor.GetComponent<SurvivalStats>();
            TryDrink(stats);
        }

        public void OnFocus() { }
        public void OnLoseFocus() { }

        public string CapturePersistenceState()
        {
            return JsonUtility.ToJson(new RainCatcherState
            {
                waterUnits = currentWaterUnits,
                lastHour = _lastHour
            });
        }

        public void RestorePersistenceState(string json)
        {
            RainCatcherState saved = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<RainCatcherState>(json);
            if (saved == null) return;
            currentWaterUnits = Mathf.Clamp(saved.waterUnits, 0f, maxWaterUnits);
            _lastHour = saved.lastHour;
            ApplyVisual();
            PublishNetworkState();
        }

        private void PublishNetworkState()
        {
            if (IsServer) _networkState?.PublishNow();
        }
    }
}
