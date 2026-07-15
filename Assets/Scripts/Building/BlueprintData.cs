// ─── BlueprintData.cs ─────────────────────────────────────────────────────────
using System.Collections.Generic;
using UnityEngine;

namespace TheForest.Building.Data
{
    [System.Serializable]
    public struct BlueprintPieceInfo
    {
        public string materialId;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public LogType logType;
    }

    [CreateAssetMenu(fileName = "Blueprint_", menuName = "The Forest/Building/Blueprint Data")]
    public class BlueprintData : ScriptableObject
    {
        [Header("Identity")]
        public string blueprintId;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;
        public bool isHidden = false;

        [Header("Prefabs")]
        public GameObject ghostPrefab;
        public GameObject completedPrefab;

        [Header("Cost")]
        public List<MaterialCost> materialCosts = new();

        [Header("Layout")]
        public List<BlueprintPieceInfo> pieces = new();

        [Header("Catalogue")]
        public string category = "Shelter";

        public int TotalMaterialCount
        {
            get { int t = 0; foreach (var c in materialCosts) t += c.amount; return t; }
        }

        // ── Known log costs from Data 18 (logs / sticks / rope / tarp) ────────
        public static readonly Dictionary<string, (int logs, int sticks, int rope, int tarp)> KnownCosts = new()
        {
            { "tarp_tent",       (  0,  2, 0, 1) },
            { "lean_to",         ( 53,  0, 0, 0) },
            { "small_log_cabin", ( 75,  0, 0, 0) },
            { "lookout_tower",   ( 60,  0, 1, 0) },
            { "tree_platform_1", (  7,  0, 1, 0) },
            { "tree_shelter_1",  ( 70,  0, 1, 0) },
            { "tree_shelter_2",  ( 96,  0, 1, 0) },
        };
    }
}