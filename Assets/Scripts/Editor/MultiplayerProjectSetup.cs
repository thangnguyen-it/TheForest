#if UNITY_EDITOR
using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using TheForest.Multiplayer;

namespace TheForest.EditorTools
{
    public static class MultiplayerProjectSetup
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        private const string NetworkListPath = "Assets/Resources/Networking/DefaultNetworkPrefabs.asset";

        [MenuItem("The Forest/Multiplayer/Configure Player Prefab")]
        public static void ConfigurePlayerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                if (root.GetComponent<NetworkObject>() == null) root.AddComponent<NetworkObject>();
                if (root.GetComponent<OwnerNetworkTransform>() == null) root.AddComponent<OwnerNetworkTransform>();
                if (root.GetComponent<NetworkPlayerOwnership>() == null) root.AddComponent<NetworkPlayerOwnership>();
                if (root.GetComponent<NetworkPlayerStateSync>() == null) root.AddComponent<NetworkPlayerStateSync>();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(NetworkListPath));
            NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, NetworkListPath);
            }

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (!list.Contains(playerPrefab))
                list.Add(new NetworkPrefab { Override = NetworkPrefabOverride.None, Prefab = playerPrefab });

            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MultiplayerProjectSetup] Player network prefab configured.");
        }
    }
}
#endif
