using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TheForest.World;

namespace TheForest.Multiplayer
{
    /// <summary>Host-authoritative clock, season and weather replication without requiring a network prefab.</summary>
    [DisallowMultipleComponent]
    public sealed class NetworkWorldSynchronizer : MonoBehaviour
    {
        private const string WorldStateMessage = "sotf/world-state/v1";

        [SerializeField, Min(0.1f)] private float synchronizationInterval = 1f;

        private NetworkManager _networkManager;
        private DayNightCycle _clock;
        private SeasonSystem _seasons;
        private WeatherSystem _weather;
        private float _timer;
        private bool _registered;

        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
        }

        private void Update()
        {
            if (_networkManager == null || !_networkManager.IsListening)
            {
                Unregister();
                return;
            }

            RegisterIfNeeded();
            ResolveWorldReferences();

            if (!_networkManager.IsServer)
            {
                if (_clock != null) _clock.enabled = false;
                return;
            }

            if (_clock != null && !_clock.enabled) _clock.enabled = true;
            _timer += Time.unscaledDeltaTime;
            if (_timer < synchronizationInterval) return;
            _timer = 0f;
            BroadcastWorldState();
        }

        private void OnDestroy()
        {
            Unregister();
            if (_clock != null) _clock.enabled = true;
        }

        private void RegisterIfNeeded()
        {
            if (_registered || _networkManager.CustomMessagingManager == null) return;
            _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(WorldStateMessage, ReceiveWorldState);
            _networkManager.OnClientConnectedCallback += SendInitialState;
            _registered = true;
        }

        private void Unregister()
        {
            if (!_registered || _networkManager == null) return;
            if (_networkManager.CustomMessagingManager != null)
                _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(WorldStateMessage);
            _networkManager.OnClientConnectedCallback -= SendInitialState;
            _registered = false;
            if (_clock != null) _clock.enabled = true;
        }

        private void ResolveWorldReferences()
        {
            if (_clock == null) _clock = FindFirstObjectByType<DayNightCycle>(FindObjectsInactive.Include);
            if (_seasons == null) _seasons = FindFirstObjectByType<SeasonSystem>(FindObjectsInactive.Include);
            if (_weather == null) _weather = FindFirstObjectByType<WeatherSystem>(FindObjectsInactive.Include);
        }

        private void BroadcastWorldState()
        {
            foreach (ulong clientId in _networkManager.ConnectedClientsIds)
            {
                if (clientId != NetworkManager.ServerClientId) SendWorldState(clientId);
            }
        }

        private void SendInitialState(ulong clientId)
        {
            if (_networkManager.IsServer && clientId != NetworkManager.ServerClientId)
                SendWorldState(clientId);
        }

        private void SendWorldState(ulong clientId)
        {
            ResolveWorldReferences();
            using var writer = new FastBufferWriter(32, Allocator.Temp);
            writer.WriteValueSafe(_clock != null ? _clock.DayNumber : 1);
            writer.WriteValueSafe(_clock != null ? _clock.CurrentHour : 8f);
            writer.WriteValueSafe(_seasons != null ? (int)_seasons.CurrentSeason : 0);
            writer.WriteValueSafe(_seasons != null ? _seasons.SeasonDay : 1);
            writer.WriteValueSafe(_weather != null && _weather.IsRaining);
            _networkManager.CustomMessagingManager.SendNamedMessage(
                WorldStateMessage, clientId, writer, NetworkDelivery.ReliableSequenced);
        }

        private void ReceiveWorldState(ulong senderClientId, FastBufferReader reader)
        {
            if (_networkManager.IsServer || senderClientId != NetworkManager.ServerClientId) return;

            reader.ReadValueSafe(out int dayNumber);
            reader.ReadValueSafe(out float hour);
            reader.ReadValueSafe(out int season);
            reader.ReadValueSafe(out int seasonDay);
            reader.ReadValueSafe(out bool raining);

            ResolveWorldReferences();
            if (_clock != null) _clock.RestoreTime(dayNumber, hour);
            if (_seasons != null)
                _seasons.ForceSeason((WorldSeason)Mathf.Clamp(season, 0, 3), seasonDay);
            if (_weather != null) _weather.SetRaining(raining);
        }
    }
}
