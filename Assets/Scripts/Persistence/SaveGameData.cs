using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheForest.Persistence
{
    [Serializable]
    public struct SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public SerializableVector3(Vector3 value)
        {
            x = value.x;
            y = value.y;
            z = value.z;
        }

        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    [Serializable]
    public struct SerializableQuaternion
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public SerializableQuaternion(Quaternion value)
        {
            x = value.x;
            y = value.y;
            z = value.z;
            w = value.w;
        }

        public Quaternion ToQuaternion() => new Quaternion(x, y, z, w);
    }

    [Serializable]
    public sealed class InventoryItemSaveData
    {
        public string itemId;
        public int amount;
    }

    [Serializable]
    public sealed class SurvivalSaveData
    {
        public float hunger;
        public float thirst;
        public float energy;
        public float stamina;
        public float health;
        public float temperature;
        public bool isWet;
        public bool hasDiedBefore;
    }

    [Serializable]
    public sealed class PlayerSaveData
    {
        public string playerId = "local";
        public SerializableVector3 position;
        public SerializableQuaternion rotation;
        public SurvivalSaveData survival = new SurvivalSaveData();
        public List<InventoryItemSaveData> inventory = new List<InventoryItemSaveData>();
    }

    [Serializable]
    public sealed class WorldSaveData
    {
        public int dayNumber = 1;
        public float hour = 8f;
        public int season;
        public int seasonDay = 1;
        public bool isRaining;
        public List<BuildingPieceSaveData> buildingPieces = new List<BuildingPieceSaveData>();
        public List<WorldObjectSaveData> worldObjects = new List<WorldObjectSaveData>();
    }

    [Serializable]
    public sealed class WorldObjectSaveData
    {
        public string persistenceId;
        public string participantType;
        public string json;
    }

    [Serializable]
    public sealed class BuildingPieceSaveData
    {
        public string prefabId;
        public int logType;
        public int orientation;
        public SerializableVector3 position;
        public SerializableQuaternion rotation;
        public SerializableVector3 scale;
        public float maxHealth;
        public float currentHealth;
        public bool isSpiked;
        public bool isImmutable;
    }

    [Serializable]
    public sealed class SaveGameData
    {
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        public string saveId;
        public string savedAtUtc;
        public string sceneName;
        public WorldSaveData world = new WorldSaveData();
        public List<PlayerSaveData> players = new List<PlayerSaveData>();
    }
}
