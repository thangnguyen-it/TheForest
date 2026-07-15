using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TheForest.Persistence;

namespace TheForest.Multiplayer
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkWorldObjectState : NetworkBehaviour
    {
        [SerializeField, Min(0.05f)] private float publishInterval = 0.25f;

        private readonly NetworkVariable<FixedString4096Bytes> _state =
            new NetworkVariable<FixedString4096Bytes>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private IPersistentStateParticipant _participant;
        private string _lastPublished = string.Empty;
        private float _timer;

        private void Awake()
        {
            _participant = GetComponent<IPersistentStateParticipant>();
        }

        public override void OnNetworkSpawn()
        {
            _state.OnValueChanged += HandleStateChanged;

            if (IsServer)
            {
                PublishNow();
            }
            else
            {
                ApplyRemoteState(_state.Value.ToString());
            }
        }

        public override void OnNetworkDespawn()
        {
            _state.OnValueChanged -= HandleStateChanged;
        }

        private void Update()
        {
            if (!IsServer || _participant == null) return;

            _timer += Time.unscaledDeltaTime;
            if (_timer < publishInterval) return;

            _timer = 0f;
            PublishNow();
        }

        public void PublishNow()
        {
            if (!IsServer || _participant == null) return;

            string json = _participant.CapturePersistenceState();
            if (string.IsNullOrEmpty(json) || json == _lastPublished || json.Length > 4096) return;

            _lastPublished = json;
            _state.Value = new FixedString4096Bytes(json);
        }

        private void HandleStateChanged(FixedString4096Bytes previous, FixedString4096Bytes current)
        {
            if (IsServer) return;
            ApplyRemoteState(current.ToString());
        }

        private void ApplyRemoteState(string json)
        {
            if (_participant == null || string.IsNullOrEmpty(json)) return;
            _participant.RestorePersistenceState(json);
        }
    }
}
