using System;
using UnityEngine;

namespace TheForest.Player
{
    /// <summary>
    /// Hệ thống sinh tồn cốt lõi của người chơi.
    /// Mô hình The Forest: các chỉ số KHÔNG độc lập mà liên kết theo chuỗi nhân quả.
    /// Thứ tự xử lý mỗi tick: Temperature -> Cold -> Energy -> Stamina(kẹp trần) -> Health -> Burning -> GreyZone.
    /// Các hệ thống khác (HUD, hiệu ứng) lắng nghe qua event, không nhồi logic UI vào đây.
    /// </summary>
    public class SurvivalStats : MonoBehaviour
    {
        // ===================== HẰNG SỐ NGƯỠNG (theo tài liệu) =====================
        private const float EnergyLowWarning = 30f;   // HUD cảnh báo "Năng lượng thấp"
        private const float FrostDelay = 8f;    // đứng yên trong Cold đủ 8s -> frost damage
        private const float GreyZonePercent = 0.10f; // dưới 10% máu -> màn hình xám

        // ===================== HUNGER (Đói) =====================
        [Header("Hunger - Đói")]
        [SerializeField] private float hungerMax = 100f;
        [SerializeField] private float hunger = 100f;
        [SerializeField] private float hungerDecayPerSec = 0.15f;   // giảm dần theo thời gian
        [SerializeField] private float starveDamagePerSec = 1.5f;   // cạn -> mất máu

        // ===================== THIRST (Khát) =====================
        [Header("Thirst - Khát")]
        [SerializeField] private float thirstMax = 100f;
        [SerializeField] private float thirst = 100f;
        [SerializeField] private float thirstDecayPerSec = 0.22f;   // khát nhanh hơn đói
        [SerializeField] private float dehydrateDamagePerSec = 1.5f;

        // ===================== ENERGY / FATIGUE (Năng lượng) =====================
        [Header("Energy - Năng lượng (chỉ số phức tạp nhất)")]
        [SerializeField] private float energyMax = 100f;
        [SerializeField] private float energy = 100f;
        [SerializeField] private float energyIdleDecayPerSec = 0.05f; // hao nền chậm
        [SerializeField] private float energyColdDrainPerSec = 0.5f;  // lạnh+ướt làm hao nhanh hơn
        [SerializeField] private float energyHungryDrainPerSec = 0.3f; // đói cũng làm hao
        [SerializeField] private float energyRestRecoveryPerSec = 0.35f;

        // ===================== TEMPERATURE (Nhiệt độ) =====================
        [Header("Temperature - Nhiệt độ")]
        [Tooltip("Nguồn cấp từ môi trường: <0 = lạnh. Set bởi WeatherSystem/lửa sau này.")]
        [SerializeField] private float temperature = 20f;     // độ ấm hiện tại (tạm)
        [SerializeField] private float coldThreshold = 5f;    // dưới ngưỡng này -> Cold
        [SerializeField] private float frostDamagePerSec = 2f;

        // ===================== STAMINA =====================
        [Header("Stamina (trần bị Energy giới hạn)")]
        [SerializeField] private float staminaMax = 100f;
        [SerializeField] private float stamina = 100f;
        [SerializeField] private float staminaRegenPerSec = 12f;
        [SerializeField] private float staminaSprintCostPerSec = 15f;

        // ===================== HEALTH (Máu) =====================
        [Header("Health - Máu")]
        [SerializeField] private float healthMax = 100f;
        [SerializeField] private float health = 100f;

        [Header("Cháy (Burning)")]
        [Tooltip("DoT khi đang cháy (máu/giây).")]
        [SerializeField] private float burningDamagePerSec = 4f;

        public bool IsBurning { get; private set; }
        private float _burningTimer;

        public event Action<bool> OnBurningChanged;


        // ===================== TRẠNG THÁI RUNTIME =====================
        public bool IsCold { get; private set; }
        public bool InGreyZone { get; private set; }
        private float _frostTimer;       // đếm thời gian trong Cold khi đứng yên
        private bool _isResting;         // ngồi ghế / không hoạt động nặng
        private bool _hasDiedBefore;     // cơ chế "chết lần đầu tỉnh lại trong hang"

        // Cờ ngoài cấp vào (set bởi PlayerController, WeatherSystem...)
        public bool IsWet;               // dưới mưa không lửa
        public bool IsSprinting;         // đang sprint (set bởi controller)
        public bool IsStill;             // đứng yên (cho frost timer)
        public bool IsCrouching;         // đang ngồi/rón (set bởi controller) — lõi tàng hình SotF thật

        // ===================== EVENT cho HUD / hiệu ứng =====================
        public event Action<float, float> OnHungerChanged;   // (current, max)
        public event Action<float, float> OnThirstChanged;
        public event Action<float, float> OnEnergyChanged;
        public event Action<float, float> OnStaminaChanged;
        public event Action<float, float> OnHealthChanged;
        public event Action<bool> OnColdStateChanged;
        public event Action<bool> OnWetStateChanged;
        public event Action<float> OnTemperatureChanged;
        public event Action OnEnergyLowWarning;
        public event Action OnEnterGreyZone;
        public event Action OnDeath;

        // Thuộc tính đọc nhanh
        public float HungerNormalized => hunger / hungerMax;
        public float ThirstNormalized => thirst / thirstMax;
        public float EnergyNormalized => energy / energyMax;
        public float HealthNormalized => health / healthMax;
        public float HungerCurrent => hunger;
        public float ThirstCurrent => thirst;
        public float EnergyCurrent => energy;
        public float HealthCurrent => health;
        public float Temperature => temperature;
        public bool CanExert => energy > 0f; // hết energy: không sprint/đánh/chặt cây
        public float StaminaCurrent => stamina;
        public float StaminaNormalized => stamina / staminaMax;

        private float HealthRegenMult =>
            TheForest.World.GameDifficulty.Current != null
                ? TheForest.World.GameDifficulty.Current.healthRegenMult : 1f;

        private void Update()
        {
            float dt = Time.deltaTime;

            TickHungerThirst(dt);
            TickTemperature(dt);
            TickEnergy(dt);
            TickStamina(dt);
            TickHealth(dt);
            TickBurning(dt);
            CheckGreyZoneAndDeath();
        }

        // 1) Đói & khát giảm dần

        private void TickHungerThirst(float dt)
        {
            hunger = Mathf.Max(0f, hunger - hungerDecayPerSec * dt);
            thirst = Mathf.Max(0f, thirst - thirstDecayPerSec * dt);
            OnHungerChanged?.Invoke(hunger, hungerMax);
            OnThirstChanged?.Invoke(thirst, thirstMax);
        }

        // 2) Tính trạng thái Cold + frost timer
        private void TickTemperature(float dt)
        {
            bool wasCold = IsCold;
            // Lạnh khi nhiệt độ thấp, nặng hơn nếu ướt
            float effectiveTemp = temperature - (IsWet ? 8f : 0f);
            IsCold = effectiveTemp < coldThreshold;

            if (IsCold != wasCold) OnColdStateChanged?.Invoke(IsCold);

            // Đứng yên trong Cold đủ 8s -> bắt đầu frost damage (xử lý ở TickHealth)
            if (IsCold && IsStill) _frostTimer += dt;
            else _frostTimer = 0f;
        }

        // 3) Energy: hao bởi lạnh, đói, hoạt động; KHÔNG trực tiếp gây mất máu
        private void TickEnergy(float dt)
        {
            float drain = energyIdleDecayPerSec;
            if (IsCold) drain += energyColdDrainPerSec;
            if (hunger <= 0f) drain += energyHungryDrainPerSec;
            // TODO: cộng thêm activity cost khi chặt cây/đánh nhau (chờ tài liệu crafting/combat)

            float before = energy;
            float restRecovery = _isResting && !IsCold && hunger > 0f && thirst > 0f ? energyRestRecoveryPerSec : 0f;
            energy = Mathf.Clamp(energy + (restRecovery - drain) * dt, 0f, energyMax);
            OnEnergyChanged?.Invoke(energy, energyMax);

            if (before > EnergyLowWarning && energy <= EnergyLowWarning)
                OnEnergyLowWarning?.Invoke();
        }

        // 4) Stamina: trần bị Energy giới hạn (cơ chế lock quan trọng)
        private void TickStamina(float dt)
        {
            // Trần hiện tại = phần trăm energy còn lại * staminaMax
            float staminaCap = (energy / energyMax) * staminaMax;

            if (IsSprinting && CanExert)
                stamina -= staminaSprintCostPerSec * dt;
            else
                stamina += staminaRegenPerSec * dt;

            // Không bao giờ hồi vượt trần do Energy đặt ra
            stamina = Mathf.Clamp(stamina, 0f, staminaCap);
            OnStaminaChanged?.Invoke(stamina, staminaMax);
        }

        // 5) Health: chịu hậu quả từ đói/khát cạn + frost damage. Energy KHÔNG trực tiếp trừ máu.
        private void TickHealth(float dt)
        {
            float damage = 0f;
            if (hunger <= 0f) damage += starveDamagePerSec;
            if (thirst <= 0f) damage += dehydrateDamagePerSec;
            if (_frostTimer >= FrostDelay) damage += frostDamagePerSec;

            if (damage > 0f) ApplyDamage(damage * dt);
        }

        // 6) Xử lý sát thương cháy theo thời gian (DoT)
        private void TickBurning(float dt)
        {
            if (IsWet && IsBurning)
            {
                Extinguish();
                return;
            }

            if (_burningTimer > 0f)
            {
                _burningTimer -= dt;
                ApplyDamage(burningDamagePerSec * dt);
                if (_burningTimer <= 0f) SetBurning(false);
            }
        }

        private void CheckGreyZoneAndDeath()
        {
            bool grey = HealthNormalized < GreyZonePercent && health > 0f;
            if (grey && !InGreyZone) { InGreyZone = true; OnEnterGreyZone?.Invoke(); }
            else if (!grey) InGreyZone = false;

            if (health <= 0f) Die();
        }

        // ===================== API CÔNG KHAI (cho item, lửa, ngủ...) =====================

        public void ApplyDamage(float amount)
        {
            if (health <= 0f) return;
            health = Mathf.Max(0f, health - amount);
            OnHealthChanged?.Invoke(health, healthMax);
        }

        public void ConsumeStamina(float amount)
        {
            if (amount <= 0f) return;
            stamina = Mathf.Max(0f, stamina - amount);
            OnStaminaChanged?.Invoke(stamina, staminaMax);
        }

        /// <summary>Ăn: hồi đói, có thể hồi chút máu/energy (theo tài liệu).</summary>
        public void Eat(float hungerRestore, float energyRestore = 0f, float healthRestore = 0f)
        {
            hunger = Mathf.Clamp(hunger + hungerRestore, 0f, hungerMax);
            energy = Mathf.Clamp(energy + energyRestore, 0f, energyMax);
            if (healthRestore >= 0f) Heal(healthRestore);
            else ApplyDamage(-healthRestore);
            OnHungerChanged?.Invoke(hunger, hungerMax);
            OnEnergyChanged?.Invoke(energy, energyMax);
        }

        public void Drink(float thirstRestore)
        {
            thirst = Mathf.Clamp(thirst + thirstRestore, 0f, thirstMax);
            OnThirstChanged?.Invoke(thirst, thirstMax);
        }

        public void ConsumeItem(TheForest.Items.ItemData item)
        {
            if (item == null) return;

            Eat(item.hungerRestore, item.energyRestore, item.healthRestore);
            Drink(item.thirstRestore);
        }

        /// <summary>Ngủ: cách DUY NHẤT hồi máu không tốn vật phẩm + hồi energy.</summary>
        public void Sleep(float healthRestore, float energyRestore)
        {
            Heal(healthRestore * HealthRegenMult);   // hồi máu khi ngủ scale theo difficulty
            energy = Mathf.Min(energyMax, energy + energyRestore);
            OnEnergyChanged?.Invoke(energy, energyMax);
        }

        /// <summary>Dùng thuốc/thảo dược (aloe, coneflower, pills) hồi máu.</summary>
        public void Heal(float amount)
        {
            if (amount <= 0f || health <= 0f) return;
            health = Mathf.Min(healthMax, health + amount);
            OnHealthChanged?.Invoke(health, healthMax);
        }

        public void SetTemperature(float value)
        {
            if (Mathf.Approximately(temperature, value)) return;
            temperature = value;
            OnTemperatureChanged?.Invoke(temperature);
        }

        public void SetWet(bool wet)
        {
            if (IsWet == wet) return;
            IsWet = wet;
            OnWetStateChanged?.Invoke(IsWet);
        }

        /// <summary>Restores a trusted snapshot from disk or the authoritative host.</summary>
        public void RestoreState(float savedHunger, float savedThirst, float savedEnergy,
            float savedStamina, float savedHealth, float savedTemperature, bool savedWet,
            bool hasDiedBefore)
        {
            hunger = Mathf.Clamp(savedHunger, 0f, hungerMax);
            thirst = Mathf.Clamp(savedThirst, 0f, thirstMax);
            energy = Mathf.Clamp(savedEnergy, 0f, energyMax);
            stamina = Mathf.Clamp(savedStamina, 0f, staminaMax);
            health = Mathf.Clamp(savedHealth, 0f, healthMax);
            temperature = savedTemperature;
            IsWet = savedWet;
            _hasDiedBefore = hasDiedBefore;
            _frostTimer = 0f;

            OnHungerChanged?.Invoke(hunger, hungerMax);
            OnThirstChanged?.Invoke(thirst, thirstMax);
            OnEnergyChanged?.Invoke(energy, energyMax);
            OnStaminaChanged?.Invoke(stamina, staminaMax);
            OnHealthChanged?.Invoke(health, healthMax);
            OnTemperatureChanged?.Invoke(temperature);
            OnWetStateChanged?.Invoke(IsWet);
        }

        public bool HasDiedBefore => _hasDiedBefore;

        // Thêm vào cạnh hàm SetResting() hoặc Update()
        public void ApplyChopFatigue(float fatigueAmount)
        {
            energy = Mathf.Max(0f, energy - fatigueAmount);
        }

        public void SetResting(bool resting) => _isResting = resting;

        /// <summary>Đốt cháy player trong 'duration' giây (gia hạn nếu đang cháy).</summary>
        public void Ignite(float duration)
        {
            _burningTimer = Mathf.Max(_burningTimer, duration);
            SetBurning(true);
        }

        public void Extinguish() // dập lửa (vd xuống nước)
        {
            _burningTimer = 0f;
            SetBurning(false);
        }

        private void SetBurning(bool value)
        {
            if (IsBurning == value) return;
            IsBurning = value;
            OnBurningChanged?.Invoke(IsBurning);
        }

        private void Die()
        {
            // Cơ chế đặc trưng: lần chết đầu (không phải do cá mập) -> tỉnh lại trong hang
            if (!_hasDiedBefore)
            {
                _hasDiedBefore = true;
                // TODO: GameManager teleport vào hang + set máu/đói/energy thấp thay vì game over
                health = healthMax * 0.2f;
                hunger = hungerMax * 0.2f;
                energy = energyMax * 0.2f;
                OnHealthChanged?.Invoke(health, healthMax);
                return;
            }
            OnDeath?.Invoke();
        }
    }
}
