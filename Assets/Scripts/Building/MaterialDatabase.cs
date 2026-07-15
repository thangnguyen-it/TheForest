// ─── MaterialDatabase.cs ──────────────────────────────────────────────────────
using System.Collections.Generic;
using UnityEngine;

namespace TheForest.Building.Data
{
    [System.Serializable]
    public struct MaterialCost
    {
        public string materialId;
        public int amount;
    }

    [CreateAssetMenu(fileName = "MaterialDatabase", menuName = "The Forest/Building/Material Database")]
    public class MaterialDatabase : ScriptableObject
    {
        public static MaterialDatabase Active { get; private set; }

        [Header("Log Prefabs")]
        public GameObject fullLogPrefab;
        public GameObject threeQuarterLogPrefab;
        public GameObject halfLogPrefab;
        public GameObject quarterLogPrefab;
        public GameObject splitLogPrefab;
        public GameObject splitQuarterPrefab;
        public GameObject stickPrefab;
        public GameObject largeSttonePrefab;

        private readonly Dictionary<string, GameObject> _lookup = new();
        private bool _built;

        private void OnEnable() { Active = this; _built = false; Build(); }
        private void OnValidate() { _built = false; Build(); }

        private void Build()
        {
            if (_built) return;
            _lookup.Clear();
            Reg("full_log", fullLogPrefab);
            Reg("three_quarter_log", threeQuarterLogPrefab);
            Reg("half_log", halfLogPrefab);
            Reg("quarter_log", quarterLogPrefab);
            Reg("split_log", splitLogPrefab);
            Reg("split_quarter", splitQuarterPrefab);
            Reg("stick", stickPrefab);
            Reg("large_stone", largeSttonePrefab);
            _built = true;
        }

        private void Reg(string id, GameObject prefab)
        { if (prefab != null) _lookup[id] = prefab; }

        public GameObject GetPrefab(string id)
        { Build(); _lookup.TryGetValue(id, out var p); return p; }

        public static LogType LogTypeForId(string id) => id switch
        {
            "full_log" => LogType.Full,
            "three_quarter_log" => LogType.ThreeQuarter,
            "half_log" => LogType.Half,
            "quarter_log" => LogType.Quarter,
            "split_log" => LogType.Split,
            "split_quarter" => LogType.SplitQuarter,
            "stick" => LogType.Stick,
            "large_stone" => LogType.Stone,
            _ => LogType.Full
        };

        public static string IdForLogType(LogType type) => type switch
        {
            LogType.ThreeQuarter => "three_quarter_log",
            LogType.Half => "half_log",
            LogType.Quarter => "quarter_log",
            LogType.Split => "split_log",
            LogType.SplitQuarter => "split_quarter",
            LogType.Stick => "stick",
            LogType.Stone => "large_stone",
            _ => "full_log"
        };
    }
}
