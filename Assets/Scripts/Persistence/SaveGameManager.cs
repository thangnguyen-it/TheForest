using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using TheForest.Items;
using TheForest.Player;
using TheForest.World;
using TheForest.Building.Core;
using TheForest.Building.Data;
using TheForest.Building.Events;
using BuildingDamageState = TheForest.Building.DamageState;
using BuildingLogType = TheForest.Building.LogType;
using BuildingPlacementMode = TheForest.Building.PlacementMode;
using TheForest.Multiplayer;

namespace TheForest.Persistence
{
    /// <summary>Captures and restores the authoritative world plus the local player profile.</summary>
    public sealed class SaveGameManager : MonoBehaviour
    {
        public const string DefaultSlot = "autosave";
        public static SaveGameManager Instance { get; private set; }

        [SerializeField, Min(0f)] private float autosaveIntervalSeconds = 300f;
        [SerializeField] private bool saveOnApplicationQuit = true;

        private SaveFileStore _store;
        private float _autosaveTimer;
        private bool _isLoading;
        private Dictionary<string, ItemData> _itemsById;
        private SaveGameData _loadedData;

        public string LastError { get; private set; }
        public string SaveDirectory => _store?.RootPath;
        public bool IsLoading => _isLoading;

        public event Action<string> OnSaved;
        public event Action<string> OnLoaded;
        public event Action<string> OnSaveFailed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntime()
        {
            if (FindFirstObjectByType<SaveGameManager>() != null) return;
            new GameObject("Save Game Runtime").AddComponent<SaveGameManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _store = new SaveFileStore();
        }

        private void Update()
        {
            if (_isLoading || autosaveIntervalSeconds <= 0f) return;
            _autosaveTimer += Time.unscaledDeltaTime;
            if (_autosaveTimer < autosaveIntervalSeconds) return;
            _autosaveTimer = 0f;
            Save(DefaultSlot);
        }

        private void OnApplicationQuit()
        {
            if (saveOnApplicationQuit && !_isLoading && FindSavePlayer() != null) Save(DefaultSlot);
        }

        public bool HasSave(string slot = DefaultSlot) => _store.Exists(slot);

        public bool Save(string slot = DefaultSlot)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening &&
                NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                LastError = "Only the host/server can save the shared world.";
                OnSaveFailed?.Invoke(LastError);
                return false;
            }

            if (FindSavePlayer() == null)
            {
                LastError = "No locally owned player is available to save.";
                return false;
            }

            try
            {
                SaveGameData data = Capture(slot);
                _store.Write(slot, data);
                _loadedData = data;
                LastError = null;
                OnSaved?.Invoke(slot);
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                Debug.LogError($"[SaveGame] Could not save slot '{slot}': {exception}", this);
                OnSaveFailed?.Invoke(LastError);
                return false;
            }
        }

        public void Load(string slot = DefaultSlot)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                LastError = NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer
                    ? "Only the host/server can load the shared world."
                    : "Stop the co-op session before loading a world, then host the restored save.";
                OnSaveFailed?.Invoke(LastError);
                return;
            }
            if (!_isLoading) StartCoroutine(LoadRoutine(slot));
        }

        private IEnumerator LoadRoutine(string slot)
        {
            _isLoading = true;
            SaveGameData data;
            try
            {
                data = _store.Read(slot);
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                Debug.LogError($"[SaveGame] Could not load slot '{slot}': {exception}", this);
                OnSaveFailed?.Invoke(LastError);
                _isLoading = false;
                yield break;
            }

            if (!string.IsNullOrEmpty(data.sceneName) &&
                !string.Equals(SceneManager.GetActiveScene().name, data.sceneName, StringComparison.Ordinal))
            {
                AsyncOperation operation = SceneManager.LoadSceneAsync(data.sceneName);
                if (operation == null)
                {
                    LastError = $"Scene '{data.sceneName}' is not available in Build Settings.";
                    OnSaveFailed?.Invoke(LastError);
                    _isLoading = false;
                    yield break;
                }
                while (!operation.isDone) yield return null;
            }

            yield return null;
            _loadedData = data;
            Restore(data);
            _autosaveTimer = 0f;
            _isLoading = false;
            LastError = null;
            OnLoaded?.Invoke(slot);
        }

        private SaveGameData Capture(string slot)
        {
            var data = new SaveGameData
            {
                saveId = slot,
                savedAtUtc = DateTime.UtcNow.ToString("O"),
                sceneName = SceneManager.GetActiveScene().name,
                world = CaptureWorld()
            };

            var capturedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SurvivalStats stats in FindObjectsByType<SurvivalStats>(FindObjectsSortMode.None))
            {
                if (stats == null) continue;
                NetworkObject networkObject = stats.GetComponent<NetworkObject>();
                if (networkObject != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening &&
                    !networkObject.IsSpawned) continue;

                string playerId = ResolvePlayerId(stats);
                if (string.IsNullOrEmpty(playerId) || !capturedIds.Add(playerId)) continue;
                data.players.Add(CapturePlayer(stats, playerId));
            }

            if (data.players.Count == 0)
                throw new InvalidOperationException("No player profile is available to save.");

            if (_loadedData?.players != null)
            {
                foreach (PlayerSaveData offline in _loadedData.players)
                {
                    if (offline != null && !string.IsNullOrEmpty(offline.playerId) && capturedIds.Add(offline.playerId))
                        data.players.Add(offline);
                }
            }
            return data;
        }

        private static WorldSaveData CaptureWorld()
        {
            DayNightCycle clock = FindFirstObjectByType<DayNightCycle>();
            SeasonSystem seasons = FindFirstObjectByType<SeasonSystem>();
            WeatherSystem weather = FindFirstObjectByType<WeatherSystem>();

            var world = new WorldSaveData
            {
                dayNumber = clock != null ? clock.DayNumber : 1,
                hour = clock != null ? clock.CurrentHour : 8f,
                season = seasons != null ? (int)seasons.CurrentSeason : 0,
                seasonDay = seasons != null ? seasons.SeasonDay : 1,
                isRaining = weather != null && weather.IsRaining
            };

            foreach (LogPiece piece in FindObjectsByType<LogPiece>(FindObjectsSortMode.None))
            {
                if (piece == null || piece.DamageState == BuildingDamageState.Destroyed) continue;
                world.buildingPieces.Add(new BuildingPieceSaveData
                {
                    prefabId = MaterialDatabase.IdForLogType(piece.LogType),
                    logType = (int)piece.LogType,
                    orientation = (int)piece.Orientation,
                    position = new SerializableVector3(piece.transform.position),
                    rotation = new SerializableQuaternion(piece.transform.rotation),
                    scale = new SerializableVector3(piece.transform.localScale),
                    maxHealth = piece.MaxHealth,
                    currentHealth = piece.CurrentHealth,
                    isSpiked = piece.IsSpiked,
                    isImmutable = piece.IsImmutable
                });
            }

            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour is not IPersistentStateParticipant participant) continue;
                string persistenceId = participant.PersistenceId;
                if (string.IsNullOrEmpty(persistenceId)) continue;
                world.worldObjects.Add(new WorldObjectSaveData
                {
                    persistenceId = persistenceId,
                    participantType = behaviour.GetType().FullName,
                    json = participant.CapturePersistenceState()
                });
            }
            return world;
        }

        private static PlayerSaveData CapturePlayer(SurvivalStats stats, string playerId)
        {
            var player = new PlayerSaveData
            {
                playerId = playerId,
                position = new SerializableVector3(stats.transform.position),
                rotation = new SerializableQuaternion(stats.transform.rotation),
                survival = new SurvivalSaveData
                {
                    hunger = stats.HungerCurrent,
                    thirst = stats.ThirstCurrent,
                    energy = stats.EnergyCurrent,
                    stamina = stats.StaminaCurrent,
                    health = stats.HealthCurrent,
                    temperature = stats.Temperature,
                    isWet = stats.IsWet,
                    hasDiedBefore = stats.HasDiedBefore
                }
            };

            Inventory inventory = stats.GetComponent<Inventory>();
            if (inventory != null)
            {
                foreach (InventorySlot inventorySlot in inventory.Slots)
                {
                    if (inventorySlot == null || inventorySlot.IsEmpty || string.IsNullOrEmpty(inventorySlot.item.itemId)) continue;
                    player.inventory.Add(new InventoryItemSaveData
                    {
                        itemId = inventorySlot.item.itemId,
                        amount = inventorySlot.count
                    });
                }
            }
            return player;
        }

        private void Restore(SaveGameData data)
        {
            DayNightCycle clock = FindFirstObjectByType<DayNightCycle>();
            if (clock != null) clock.RestoreTime(data.world.dayNumber, data.world.hour);

            SeasonSystem seasons = FindFirstObjectByType<SeasonSystem>();
            if (seasons != null)
                seasons.ForceSeason((WorldSeason)Mathf.Clamp(data.world.season, 0, 3), data.world.seasonDay);

            WeatherSystem weather = FindFirstObjectByType<WeatherSystem>();
            if (weather != null) weather.SetRaining(data.world.isRaining);

            RestoreBuildingPieces(data.world.buildingPieces);
            RestoreWorldParticipants(data.world.worldObjects);

            if (data.players == null || data.players.Count == 0) return;
            SurvivalStats stats = FindSavePlayer();
            if (stats == null) return;

            string localPlayerId = CoopSessionManager.Instance != null
                ? CoopSessionManager.Instance.LocalPlayerId
                : "local";
            PlayerSaveData player = data.players.Find(candidate => candidate != null && candidate.playerId == localPlayerId)
                                    ?? data.players[0];
            ApplyPlayerProfile(player, stats);
        }

        public bool TryRestorePlayerProfile(string playerId, SurvivalStats stats)
        {
            if (_loadedData?.players == null || stats == null || string.IsNullOrEmpty(playerId)) return false;
            PlayerSaveData profile = _loadedData.players.Find(candidate =>
                candidate != null && string.Equals(candidate.playerId, playerId, StringComparison.Ordinal));
            if (profile == null) return false;
            ApplyPlayerProfile(profile, stats);
            return true;
        }

        private void ApplyPlayerProfile(PlayerSaveData player, SurvivalStats stats)
        {
            if (player == null || stats == null) return;
            stats.transform.SetPositionAndRotation(player.position.ToVector3(), player.rotation.ToQuaternion());
            SurvivalSaveData survival = player.survival ?? new SurvivalSaveData();
            stats.RestoreState(survival.hunger, survival.thirst, survival.energy, survival.stamina,
                survival.health, survival.temperature, survival.isWet, survival.hasDiedBefore);
            RestoreInventory(stats.GetComponent<Inventory>(), player.inventory);
        }

        private void RestoreInventory(Inventory inventory, List<InventoryItemSaveData> entries)
        {
            if (inventory == null) return;
            inventory.Clear();
            EnsureItemLookup();
            if (entries == null) return;

            foreach (InventoryItemSaveData entry in entries)
            {
                if (entry == null || entry.amount <= 0 || !_itemsById.TryGetValue(entry.itemId, out ItemData item))
                {
                    Debug.LogWarning($"[SaveGame] Skipped unknown inventory item '{entry?.itemId}'.", this);
                    continue;
                }
                inventory.Add(item, entry.amount);
            }
        }

        private void EnsureItemLookup()
        {
            if (_itemsById != null) return;
            _itemsById = new Dictionary<string, ItemData>(StringComparer.Ordinal);
            foreach (ItemData item in Resources.LoadAll<ItemData>("SotFData/Items"))
            {
                if (item != null && !string.IsNullOrEmpty(item.itemId)) _itemsById[item.itemId] = item;
            }
        }

        private void RestoreBuildingPieces(List<BuildingPieceSaveData> savedPieces)
        {
            if (savedPieces == null) return;
            MaterialDatabase database = MaterialDatabase.Active;
            if (database == null)
            {
                if (savedPieces.Count > 0)
                    Debug.LogWarning("[SaveGame] MaterialDatabase is not loaded; building pieces were not restored.", this);
                return;
            }

            foreach (LogPiece existing in FindObjectsByType<LogPiece>(FindObjectsSortMode.None))
            {
                if (existing == null) continue;
                existing.gameObject.SetActive(false);
                Destroy(existing.gameObject);
            }

            foreach (BuildingPieceSaveData saved in savedPieces)
            {
                if (saved == null) continue;
                GameObject prefab = database.GetPrefab(saved.prefabId);
                if (prefab == null)
                {
                    Debug.LogWarning($"[SaveGame] Missing building prefab '{saved.prefabId}'.", this);
                    continue;
                }

                GameObject instance = Instantiate(prefab, saved.position.ToVector3(), saved.rotation.ToQuaternion());
                instance.transform.localScale = saved.scale.ToVector3();
                LogPiece piece = instance.GetComponent<LogPiece>();
                if (piece == null)
                {
                    Debug.LogWarning($"[SaveGame] Building prefab '{saved.prefabId}' has no LogPiece.", instance);
                    Destroy(instance);
                    continue;
                }

                var logType = (BuildingLogType)Mathf.Clamp(saved.logType, 0, (int)BuildingLogType.Stone);
                var orientation = (BuildingPlacementMode)Mathf.Clamp(saved.orientation, 0, (int)BuildingPlacementMode.Diagonal);
                piece.RestoreState(logType, orientation, saved.maxHealth, saved.currentHealth,
                    saved.isSpiked, saved.isImmutable);
                EventBus<LogPlacedEvent>.Raise(new LogPlacedEvent(instance, logType,
                    instance.transform.position, instance.transform.rotation, 0));
            }
        }

        private void RestoreWorldParticipants(List<WorldObjectSaveData> savedObjects)
        {
            if (savedObjects == null || savedObjects.Count == 0) return;

            var participants = new Dictionary<string, IPersistentStateParticipant>(StringComparer.Ordinal);
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour is IPersistentStateParticipant participant &&
                    !string.IsNullOrEmpty(participant.PersistenceId))
                {
                    participants[participant.PersistenceId] = participant;
                }
            }

            foreach (WorldObjectSaveData saved in savedObjects)
            {
                if (saved == null || string.IsNullOrEmpty(saved.persistenceId)) continue;
                if (!participants.TryGetValue(saved.persistenceId, out IPersistentStateParticipant participant))
                {
                    Debug.LogWarning($"[SaveGame] Persistent object '{saved.persistenceId}' ({saved.participantType}) was not found.", this);
                    continue;
                }
                participant.RestorePersistenceState(saved.json);
            }
        }

        private static SurvivalStats FindSavePlayer()
        {
            SurvivalStats fallback = null;
            foreach (SurvivalStats stats in FindObjectsByType<SurvivalStats>(FindObjectsSortMode.None))
            {
                if (stats == null) continue;
                NetworkObject networkObject = stats.GetComponent<NetworkObject>();
                if (networkObject == null) fallback ??= stats;
                else if (!networkObject.IsSpawned || networkObject.IsOwner) return stats;
            }
            return fallback;
        }

        private static string ResolvePlayerId(SurvivalStats stats)
        {
            NetworkPlayerStateSync sync = stats.GetComponent<NetworkPlayerStateSync>();
            if (sync != null && !string.IsNullOrEmpty(sync.PlayerId)) return sync.PlayerId;

            NetworkObject networkObject = stats.GetComponent<NetworkObject>();
            if (networkObject == null || !networkObject.IsSpawned || networkObject.IsOwner)
                return CoopSessionManager.Instance != null ? CoopSessionManager.Instance.LocalPlayerId : "local";
            return $"client-{networkObject.OwnerClientId}";
        }
    }
}
