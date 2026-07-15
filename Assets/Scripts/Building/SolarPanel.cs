// ═══════════════════════════════════════════════════════════════════════════
// SOLAR PANEL
// ═══════════════════════════════════════════════════════════════════════════
using TheForest.Building.Config;
using TheForest.Building.Core;
using TheForest.Building.Events;
using TheForest.Building.Systems;
using UnityEngine;

public class SolarPanel : MonoBehaviour
{
    [SerializeField] private ElectricityConfig config;
    [SerializeField] private BatteryStorage attachedBattery;
    [SerializeField] private PowerNode powerNode;

    private bool _wasGenerating;

    private void Awake()
    {
        if (powerNode == null) powerNode = GetComponent<PowerNode>();
    }

    private void OnEnable()
    {
        EventBus<RainBlockedEvent>.Subscribe(OnRoofChanged); // shadow check hook
    }

    private void OnDisable()
    {
        EventBus<RainBlockedEvent>.Unsubscribe(OnRoofChanged);
    }

    private void Update()
    {
        if (config == null) return;
        bool generating = IsGeneratingNow();

        if (generating != _wasGenerating)
        {
            _wasGenerating = generating;
            EventBus<SolarGeneratingEvent>.Raise(
                new SolarGeneratingEvent(generating, generating ? config.solarOutputWatts : 0f));
        }

        if (generating && attachedBattery != null)
            attachedBattery.Charge(config.solarOutputWatts * Time.deltaTime / 3600f); // Wh
    }

    private bool IsGeneratingNow()
    {
        if (config == null) return false;
        // Hour-of-day via DayNightCycle — accessed via a shared FloatVariable or direct ref
        // For architecture compliance, read from a shared SO variable (injected)
        float hour = _hourVariable != null ? _hourVariable.Value : 12f;
        return hour >= config.solarStartHour && hour < config.solarEndHour;
    }

    [Header("Shared hour variable (from DayNightCycle SO)")]
    [SerializeField] private Companion.Data.FloatVariableSO _hourVariable;

    private void OnRoofChanged( RainBlockedEvent e) { /* shadow logic placeholder */ }

    public float GetOutputWatts() =>
        config != null && IsGeneratingNow() ? config.solarOutputWatts : 0f;
}