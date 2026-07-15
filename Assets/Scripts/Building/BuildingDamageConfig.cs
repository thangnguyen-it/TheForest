// ═══════════════════════════════════════════════════════════════════════════════
// 2.12  BUILDING DAMAGE CONFIG
// ═══════════════════════════════════════════════════════════════════════════════
using UnityEngine;

namespace TheForest.Building.Config
{
    [CreateAssetMenu(fileName = "BuildingDamageConfig", menuName = "The Forest/Building/Damage Config")]
    public class BuildingDamageConfig : ScriptableObject
    {
        [Header("Log Health")]
        public float fullLogHealth = 100f;
        public float splitLogHealth = 55f;
        public float stickHealth = 25f;
        public float stoneHealth = 220f;

        [Header("Damage Visual Threshold")]
        [Range(0.1f, 0.9f)]
        [Tooltip("Health ratio below which piece shows damage visuals")]
        public float damagedThreshold = 0.50f;

        [Header("Incoming Damage")]
        public float cannibalSwingDamage = 15f;
        public float mutantSwingDamage = 35f;
        public float explosiveDamage = 200f;
        public float fallingTreeDamage = 80f;

        [Header("Repair Tool")]
        public float repairRatePerSec = 30f;
        public float repairHoldActivate = 0.5f;  // hold seconds before repair begins

        [Header("Spike Damage")]
        public float spikeDamageOnContact = 25f;
        public float spikeDpsWhileContact = 10f;

        [Header("Global Toggle")]
        [Tooltip("Mirror of Settings → Structure Damage toggle")]
        public bool structureDamageEnabled = true;
    }
}
