using UnityEngine;
using UnityEngine.UI;
using TheForest.Player;
using Companion.Data; // Bổ sung để nhận diện cấu trúc CompanionState của Kelvin

namespace TheForest.UI
{
    /// <summary>
    /// Điều phối HUD: 4 chỉ số survival + stamina + health, cảnh báo Energy thấp,
    /// và hiệu ứng Grey Zone (màn hình xám khi máu < 10%).
    /// Chỉ LẮNG NGHE event của SurvivalStats — không chứa logic gameplay.
    /// Tích hợp Text hiển thị tương tác + Ám sát từ InteractionRaycaster.
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        [Header("Nguồn dữ liệu")]
        [SerializeField] private SurvivalStats stats;

        [Header("Prompt tương tác")]
        [SerializeField] private TheForest.Interaction.InteractionRaycaster raycaster;
        [SerializeField] private GameObject interactPromptRoot;
        [SerializeField] private TMPro.TextMeshProUGUI interactPromptText;

        [Header("Các thanh chỉ số")]
        [SerializeField] private StatBar hungerBar;
        [SerializeField] private StatBar thirstBar;
        [SerializeField] private StatBar energyBar;
        [SerializeField] private StatBar staminaBar;
        [SerializeField] private StatBar healthBar;

        [Header("Cảnh báo & trạng thái")]
        [SerializeField] private GameObject energyLowWarning;
        [SerializeField] private GameObject coldIndicator;

        [Header("Hiệu ứng Grey Zone (máu < 10%)")]
        [Tooltip("Image phủ toàn màn hình, màu xám, alpha điều khiển bằng code")]
        [SerializeField] private Image greyZoneOverlay;
        [SerializeField] private float greyZoneFadeSpeed = 2f;

        private bool _greyZoneActive;

        private void OnEnable()
        {
            if (stats == null) return;
            stats.OnHungerChanged += hungerBar.SetValue;
            stats.OnThirstChanged += thirstBar.SetValue;
            stats.OnEnergyChanged += energyBar.SetValue;
            stats.OnStaminaChanged += staminaBar.SetValue;
            stats.OnHealthChanged += healthBar.SetValue;
            stats.OnEnergyLowWarning += HandleEnergyLow;
            stats.OnColdStateChanged += HandleColdState;
            stats.OnEnterGreyZone += HandleEnterGreyZone;
            stats.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (stats == null) return;
            stats.OnHungerChanged -= hungerBar.SetValue;
            stats.OnThirstChanged -= thirstBar.SetValue;
            stats.OnEnergyChanged -= energyBar.SetValue;
            stats.OnStaminaChanged -= staminaBar.SetValue;
            stats.OnHealthChanged -= healthBar.SetValue;
            stats.OnEnergyLowWarning -= HandleEnergyLow;
            stats.OnColdStateChanged -= HandleColdState;
            stats.OnEnterGreyZone -= HandleEnterGreyZone;
            stats.OnDeath -= HandleDeath;
        }

        private void Start()
        {
            if (energyLowWarning != null) energyLowWarning.SetActive(false);
            if (coldIndicator != null) coldIndicator.SetActive(false);
            if (greyZoneOverlay != null)
                greyZoneOverlay.color = new Color(0.5f, 0.5f, 0.5f, 0f);
        }

        private void Update()
        {
            if (energyLowWarning != null)
                energyLowWarning.SetActive(stats != null && stats.EnergyNormalized <= 0.30f);

            UpdateGreyZone();

            // Prompt tương tác (Bao gồm cả Cành cây, Đồ ăn và Á Sát quái)
            if (raycaster != null && interactPromptRoot != null)
            {
                bool show = raycaster.HasTarget;
                interactPromptRoot.SetActive(show);
                if (show && interactPromptText != null)
                {
                    interactPromptText.text = raycaster.CurrentPrompt;
                }
            }
        }

        // ---- Xử lý event hệ thống Companion ----

        /// <summary>
        /// Hàm tiếp nhận trạng thái Dynamic từ CompanionStateListener dội về.
        /// </summary>
        public void OnCompanionStateChanged(CompanionState state)
        {
            Debug.Log($"[HUDManager] Đã nhận trạng thái mới từ Kelvin: {state}");
        }

        // ---- Xử lý event nội bộ Survival ----

        private void HandleEnergyLow()
        {
            if (energyLowWarning != null) energyLowWarning.SetActive(true);
        }

        private void HandleColdState(bool isCold)
        {
            if (coldIndicator != null) coldIndicator.SetActive(isCold);
        }

        private void HandleEnterGreyZone()
        {
            _greyZoneActive = true;
        }

        private void HandleDeath()
        {
            Debug.Log("[HUD] Player đã chết.");
        }

        private void UpdateGreyZone()
        {
            if (greyZoneOverlay == null || stats == null) return;

            _greyZoneActive = stats.InGreyZone;
            float targetAlpha = _greyZoneActive ? 0.85f : 0f;

            Color c = greyZoneOverlay.color;
            c.a = Mathf.MoveTowards(c.a, targetAlpha, greyZoneFadeSpeed * Time.deltaTime);
            greyZoneOverlay.color = c;
        }
    }
}