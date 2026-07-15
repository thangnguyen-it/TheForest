using Unity.Netcode;
using UnityEngine;
using TheForest.Items;

namespace TheForest.Multiplayer
{
    public static class NetworkWorldInteraction
    {
        public static bool ShouldRouteToServer(NetworkBehaviour behaviour)
        {
            NetworkManager manager = NetworkManager.Singleton;
            return manager != null && manager.IsListening && behaviour != null && behaviour.IsSpawned && !behaviour.IsServer;
        }

        public static bool ShouldSimulateHere(NetworkBehaviour behaviour)
        {
            NetworkManager manager = NetworkManager.Singleton;
            return manager == null || !manager.IsListening || behaviour == null || !behaviour.IsSpawned || behaviour.IsServer;
        }

        public static bool TryGetPlayerObject(ulong clientId, out GameObject playerObject)
        {
            playerObject = null;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.ConnectedClients == null) return false;
            if (!manager.ConnectedClients.TryGetValue(clientId, out NetworkClient client)) return false;
            if (client.PlayerObject == null) return false;
            playerObject = client.PlayerObject.gameObject;
            return playerObject != null;
        }

        public static string ItemId(ItemData item)
        {
            return item != null ? item.itemId : string.Empty;
        }
    }
}
