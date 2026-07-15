using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using TheForest.Player;

namespace TheForest.Multiplayer
{
    /// <summary>Attach beside NetworkObject on a player prefab to disable local input/camera on remote avatars.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerOwnership : NetworkBehaviour
    {
        [SerializeField] private MonoBehaviour[] ownerOnlyBehaviours;
        [SerializeField] private Camera[] ownerOnlyCameras;
        [SerializeField] private AudioListener[] ownerOnlyAudioListeners;

        public override void OnNetworkSpawn()
        {
            ApplyOwnership(IsOwner);
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner) ApplyOwnership(true);
        }

        private void Reset()
        {
            ownerOnlyCameras = GetComponentsInChildren<Camera>(true);
            ownerOnlyAudioListeners = GetComponentsInChildren<AudioListener>(true);

            var behaviours = new System.Collections.Generic.List<MonoBehaviour>();
            AddIfPresent(behaviours, GetComponent<PlayerInput>());
            AddIfPresent(behaviours, GetComponent<PlayerController>());
            AddIfPresent(behaviours, GetComponent<PlayerLook>());
            ownerOnlyBehaviours = behaviours.ToArray();
        }

        private void ApplyOwnership(bool locallyOwned)
        {
            if (ownerOnlyBehaviours != null)
            {
                foreach (MonoBehaviour behaviour in ownerOnlyBehaviours)
                    if (behaviour != null) behaviour.enabled = locallyOwned;
            }
            if (ownerOnlyCameras != null)
            {
                foreach (Camera cameraComponent in ownerOnlyCameras)
                    if (cameraComponent != null) cameraComponent.enabled = locallyOwned;
            }
            if (ownerOnlyAudioListeners != null)
            {
                foreach (AudioListener listener in ownerOnlyAudioListeners)
                    if (listener != null) listener.enabled = locallyOwned;
            }
        }

        private static void AddIfPresent(System.Collections.Generic.List<MonoBehaviour> list, MonoBehaviour value)
        {
            if (value != null && !list.Contains(value)) list.Add(value);
        }
    }
}
