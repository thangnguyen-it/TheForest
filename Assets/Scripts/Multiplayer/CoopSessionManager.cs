using System;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

namespace TheForest.Multiplayer
{
    /// <summary>Direct/LAN co-op session entry point. Relay can replace the transport setup later.</summary>
    [DisallowMultipleComponent]
    public sealed class CoopSessionManager : MonoBehaviour
    {
        public static CoopSessionManager Instance { get; private set; }

        [Header("Session")]
        [SerializeField, Min(1)] private int maxPlayers = 8;
        [SerializeField] private ushort defaultPort = 7777;
        [SerializeField] private string protocolSignature = "sotf-clone-v1";
        [SerializeField] private string sessionPassword = string.Empty;

        private NetworkManager _networkManager;
        private UnityTransport _transport;
        private readonly Dictionary<ulong, string> _playerIdsByClient = new Dictionary<ulong, string>();
        private const string LocalPlayerIdKey = "theforest.multiplayer.player-id";

        [Serializable]
        private sealed class SessionHandshake
        {
            public string protocol;
            public string password;
            public string playerId;
        }

        public bool IsRunning => _networkManager != null && _networkManager.IsListening;
        public bool IsHost => IsRunning && _networkManager.IsHost;
        public bool IsServer => IsRunning && _networkManager.IsServer;
        public bool IsClient => IsRunning && _networkManager.IsClient;
        public int ConnectedPlayerCount => _networkManager != null ? _networkManager.ConnectedClientsIds.Count : 0;
        public string LastError { get; private set; }
        public string LocalPlayerId { get; private set; }

        public event Action OnSessionStarted;
        public event Action OnSessionStopped;
        public event Action<ulong> OnPlayerJoined;
        public event Action<ulong> OnPlayerLeft;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntime()
        {
            if (FindFirstObjectByType<CoopSessionManager>() != null) return;

            NetworkManager manager = FindFirstObjectByType<NetworkManager>();
            GameObject root;
            if (manager == null)
            {
                root = new GameObject("Co-op Network Runtime");
                root.AddComponent<UnityTransport>();
                manager = root.AddComponent<NetworkManager>();
            }
            else
            {
                root = manager.gameObject;
                if (root.GetComponent<UnityTransport>() == null) root.AddComponent<UnityTransport>();
            }

            root.AddComponent<CoopSessionManager>();
            if (root.GetComponent<NetworkWorldSynchronizer>() == null)
                root.AddComponent<NetworkWorldSynchronizer>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LocalPlayerId = LoadOrCreateLocalPlayerId();

            _networkManager = GetComponent<NetworkManager>();
            if (_networkManager == null) _networkManager = gameObject.AddComponent<NetworkManager>();
            _transport = GetComponent<UnityTransport>();
            if (_transport == null) _transport = gameObject.AddComponent<UnityTransport>();

            if (_networkManager.NetworkConfig == null) _networkManager.NetworkConfig = new NetworkConfig();
            _networkManager.NetworkConfig.NetworkTransport = _transport;
            _networkManager.NetworkConfig.ConnectionApproval = true;
            _networkManager.NetworkConfig.EnableSceneManagement = true;
            _networkManager.NetworkConfig.ProtocolVersion = 1;
            ConfigureNetworkPrefabs();
            _networkManager.ConnectionApprovalCallback = ApproveConnection;
            _networkManager.OnClientConnectedCallback += HandleClientConnected;
            _networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
            {
                _networkManager.OnClientConnectedCallback -= HandleClientConnected;
                _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
                if (_networkManager.ConnectionApprovalCallback == ApproveConnection)
                    _networkManager.ConnectionApprovalCallback = null;
            }
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (Instance == this) Instance = null;
        }

        public bool StartHost(ushort port = 0, string password = null)
        {
            if (!PrepareStart("0.0.0.0", port, password)) return false;
            bool started = _networkManager.StartHost();
            return FinishStart(started, "host");
        }

        public bool StartServer(ushort port = 0, string password = null)
        {
            if (!PrepareStart("0.0.0.0", port, password)) return false;
            bool started = _networkManager.StartServer();
            return FinishStart(started, "server");
        }

        public bool StartClient(string address, ushort port = 0, string password = null)
        {
            if (string.IsNullOrWhiteSpace(address)) address = "127.0.0.1";
            if (!PrepareStart(address.Trim(), port, password)) return false;
            bool started = _networkManager.StartClient();
            return FinishStart(started, "client");
        }

        public void Shutdown()
        {
            if (_networkManager == null || !_networkManager.IsListening) return;
            _networkManager.Shutdown();
            OnSessionStopped?.Invoke();
        }

        private bool PrepareStart(string address, ushort port, string password)
        {
            if (_networkManager == null || _transport == null)
            {
                LastError = "NetworkManager or UnityTransport is missing.";
                return false;
            }
            if (_networkManager.IsListening)
            {
                LastError = "A network session is already running.";
                return false;
            }

            sessionPassword = password ?? sessionPassword;
            ushort resolvedPort = port == 0 ? defaultPort : port;
            string listenAddress = address == "0.0.0.0" ? "0.0.0.0" : null;
            _transport.SetConnectionData(address, resolvedPort, listenAddress);
            _networkManager.NetworkConfig.ConnectionData = BuildConnectionPayload(sessionPassword);
            LastError = null;
            return true;
        }

        private bool FinishStart(bool started, string mode)
        {
            if (!started)
            {
                LastError = $"Netcode refused to start as {mode}. Check transport and player prefab configuration.";
                Debug.LogError("[CoopSession] " + LastError, this);
                return false;
            }

            OnSessionStarted?.Invoke();
            StartCoroutine(CleanupLegacyScenePlayers());
            return true;
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            SessionHandshake handshake = ParseHandshake(request.Payload);
            bool protocolMatches = handshake != null && handshake.protocol == protocolSignature;
            bool passwordMatches = handshake != null && ByteArraysEqual(
                Encoding.UTF8.GetBytes(handshake.password ?? string.Empty),
                Encoding.UTF8.GetBytes(sessionPassword ?? string.Empty));
            bool validPlayerId = handshake != null && !string.IsNullOrWhiteSpace(handshake.playerId) &&
                                 handshake.playerId.Length <= 64;
            bool duplicatePlayer = validPlayerId && _playerIdsByClient.ContainsValue(handshake.playerId) &&
                                   (!_playerIdsByClient.TryGetValue(request.ClientNetworkId, out string existingId) ||
                                    existingId != handshake.playerId);
            bool hasRoom = _networkManager.ConnectedClientsIds.Count < maxPlayers ||
                           request.ClientNetworkId == NetworkManager.ServerClientId;

            response.Approved = protocolMatches && passwordMatches && validPlayerId && !duplicatePlayer && hasRoom;
            response.CreatePlayerObject = response.Approved && _networkManager.NetworkConfig.PlayerPrefab != null;
            response.Pending = false;
            response.Reason = !protocolMatches ? "Protocol mismatch."
                : !passwordMatches ? "Session password mismatch."
                : !validPlayerId ? "Invalid player profile identity."
                : duplicatePlayer ? "This player profile is already connected."
                : !hasRoom ? "Session is full." : string.Empty;

            if (response.Approved) _playerIdsByClient[request.ClientNetworkId] = handshake.playerId;
        }

        private byte[] BuildConnectionPayload(string password)
        {
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(new SessionHandshake
            {
                protocol = protocolSignature,
                password = password ?? string.Empty,
                playerId = LocalPlayerId
            }));
        }

        public bool TryGetPlayerId(ulong clientId, out string playerId)
        {
            return _playerIdsByClient.TryGetValue(clientId, out playerId);
        }

        private static SessionHandshake ParseHandshake(byte[] payload)
        {
            if (payload == null || payload.Length == 0 || payload.Length > 1024) return null;
            try
            {
                return JsonUtility.FromJson<SessionHandshake>(Encoding.UTF8.GetString(payload));
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static string LoadOrCreateLocalPlayerId()
        {
            string playerId = PlayerPrefs.GetString(LocalPlayerIdKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(playerId)) return playerId;
            playerId = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(LocalPlayerIdKey, playerId);
            PlayerPrefs.Save();
            return playerId;
        }

        private void ConfigureNetworkPrefabs()
        {
            NetworkPrefabsList list = Resources.Load<NetworkPrefabsList>("Networking/DefaultNetworkPrefabs");
            if (list == null) return;

            if (!_networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(list))
                _networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);

            if (_networkManager.NetworkConfig.PlayerPrefab == null && list.PrefabList.Count > 0)
                _networkManager.NetworkConfig.PlayerPrefab = list.PrefabList[0].Prefab;
        }

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            int difference = 0;
            for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
            return difference == 0;
        }

        private void HandleClientConnected(ulong clientId) => OnPlayerJoined?.Invoke(clientId);
        private void HandleClientDisconnected(ulong clientId)
        {
            _playerIdsByClient.Remove(clientId);
            OnPlayerLeft?.Invoke(clientId);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsRunning) StartCoroutine(CleanupLegacyScenePlayers());
        }

        private IEnumerator CleanupLegacyScenePlayers()
        {
            // Let NGO finish spawning player and in-scene NetworkObjects first.
            yield return null;
            yield return null;

            foreach (NetworkPlayerOwnership ownership in
                     FindObjectsByType<NetworkPlayerOwnership>(FindObjectsSortMode.None))
            {
                if (ownership == null) continue;
                NetworkObject networkObject = ownership.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsSpawned && networkObject.IsPlayerObject) continue;

                if (networkObject != null && networkObject.IsSpawned)
                {
                    if (_networkManager.IsServer) networkObject.Despawn(true);
                    continue;
                }

                Destroy(ownership.gameObject);
            }
        }
    }
}
